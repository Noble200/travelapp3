using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Npgsql;
using Allva.Desktop.Models.Admin;

namespace Allva.Desktop.Services;

/// <summary>
/// Servicio para gestión de archivos de comercios
/// Maneja la subida, descarga y eliminación de archivos
/// </summary>
public class ArchivoService
{
    private const string ConnectionString = "Host=switchyard.proxy.rlwy.net;Port=55839;Database=railway;Username=postgres;Password=ysTQxChOYSWUuAPzmYQokqrjpYnKSGbk;";
    private const string CarpetaArchivos = "archivos_comercios";
    
    public ArchivoService()
    {
        // Crear carpeta si no existe
        if (!Directory.Exists(CarpetaArchivos))
        {
            Directory.CreateDirectory(CarpetaArchivos);
        }
    }
    
    /// <summary>
    /// Obtiene todos los archivos activos de un comercio
    /// </summary>
    public async Task<List<ArchivoComercioModel>> ObtenerArchivosPorComercio(int idComercio)
    {
        var archivos = new List<ArchivoComercioModel>();
        
        try
        {
            using var connection = new NpgsqlConnection(ConnectionString);
            await connection.OpenAsync();
            
            var query = @"SELECT id_archivo, id_comercio, nombre_archivo, nombre_original, 
                                 tipo_archivo, tamano_bytes, ruta_archivo, descripcion,
                                 fecha_subida, subido_por, activo
                          FROM archivos_comercios 
                          WHERE id_comercio = @IdComercio AND activo = true
                          ORDER BY fecha_subida DESC";
            
            using var cmd = new NpgsqlCommand(query, connection);
            cmd.Parameters.AddWithValue("@IdComercio", idComercio);
            
            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                archivos.Add(new ArchivoComercioModel
                {
                    IdArchivo = reader.GetInt32(0),
                    IdComercio = reader.GetInt32(1),
                    NombreArchivo = reader.GetString(2),
                    NombreOriginal = reader.GetString(3),
                    TipoArchivo = reader.IsDBNull(4) ? null : reader.GetString(4),
                    TamanoBytes = reader.IsDBNull(5) ? null : reader.GetInt64(5),
                    RutaArchivo = reader.GetString(6),
                    Descripcion = reader.IsDBNull(7) ? null : reader.GetString(7),
                    FechaSubida = reader.GetDateTime(8),
                    SubidoPor = reader.IsDBNull(9) ? null : reader.GetString(9),
                    Activo = reader.GetBoolean(10)
                });
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error obteniendo archivos: {ex.Message}");
        }
        
        return archivos;
    }
    
    /// <summary>
    /// Sube un archivo al servidor y lo registra en la base de datos
    /// </summary>
    public async Task<int> SubirArchivo(int idComercio, string rutaArchivoLocal, 
                                         string? descripcion, string usuario)
    {
        try
        {
            // Validar que el archivo existe
            if (!File.Exists(rutaArchivoLocal))
            {
                throw new FileNotFoundException("El archivo no existe", rutaArchivoLocal);
            }
            
            var nombreOriginal = Path.GetFileName(rutaArchivoLocal);
            var extension = Path.GetExtension(rutaArchivoLocal);
            var nombreUnico = $"{idComercio}_{DateTime.Now:yyyyMMdd_HHmmss}_{Guid.NewGuid().ToString("N").Substring(0, 8)}{extension}";
            var rutaDestino = Path.Combine(CarpetaArchivos, nombreUnico);
            
            // Copiar archivo
            File.Copy(rutaArchivoLocal, rutaDestino, overwrite: true);
            var tamano = new FileInfo(rutaDestino).Length;
            
            // Determinar tipo MIME
            var tipoMime = ObtenerTipoMime(extension);
            
            // Guardar en BD
            using var connection = new NpgsqlConnection(ConnectionString);
            await connection.OpenAsync();
            
            var query = @"INSERT INTO archivos_comercios 
                          (id_comercio, nombre_archivo, nombre_original, tipo_archivo, 
                           tamano_bytes, ruta_archivo, descripcion, subido_por)
                          VALUES (@IdComercio, @NombreArchivo, @NombreOriginal, @TipoArchivo,
                                  @Tamano, @Ruta, @Descripcion, @Usuario)
                          RETURNING id_archivo";
            
            using var cmd = new NpgsqlCommand(query, connection);
            cmd.Parameters.AddWithValue("@IdComercio", idComercio);
            cmd.Parameters.AddWithValue("@NombreArchivo", nombreUnico);
            cmd.Parameters.AddWithValue("@NombreOriginal", nombreOriginal);
            cmd.Parameters.AddWithValue("@TipoArchivo", tipoMime);
            cmd.Parameters.AddWithValue("@Tamano", tamano);
            cmd.Parameters.AddWithValue("@Ruta", rutaDestino);
            cmd.Parameters.AddWithValue("@Descripcion", (object?)descripcion ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@Usuario", usuario);
            
            return (int)(await cmd.ExecuteScalarAsync())!;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error subiendo archivo: {ex.Message}");
            throw;
        }
    }
    
    /// <summary>
    /// Elimina un archivo (marca como inactivo y elimina físicamente)
    /// </summary>
    public async Task<bool> EliminarArchivo(int idArchivo)
    {
        try
        {
            using var connection = new NpgsqlConnection(ConnectionString);
            await connection.OpenAsync();
            
            // Obtener ruta del archivo
            var querySelect = "SELECT ruta_archivo FROM archivos_comercios WHERE id_archivo = @Id";
            using var cmdSelect = new NpgsqlCommand(querySelect, connection);
            cmdSelect.Parameters.AddWithValue("@Id", idArchivo);
            var ruta = await cmdSelect.ExecuteScalarAsync() as string;
            
            // Eliminar archivo físico
            if (!string.IsNullOrEmpty(ruta) && File.Exists(ruta))
            {
                File.Delete(ruta);
            }
            
            // Marcar como inactivo en BD
            var queryUpdate = "UPDATE archivos_comercios SET activo = false WHERE id_archivo = @Id";
            using var cmdUpdate = new NpgsqlCommand(queryUpdate, connection);
            cmdUpdate.Parameters.AddWithValue("@Id", idArchivo);
            
            return await cmdUpdate.ExecuteNonQueryAsync() > 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error eliminando archivo: {ex.Message}");
            return false;
        }
    }
    
    /// <summary>
    /// Descarga un archivo (devuelve la ruta para abrir)
    /// </summary>
    public async Task<string?> ObtenerRutaArchivo(int idArchivo)
    {
        try
        {
            using var connection = new NpgsqlConnection(ConnectionString);
            await connection.OpenAsync();
            
            var query = "SELECT ruta_archivo FROM archivos_comercios WHERE id_archivo = @Id AND activo = true";
            using var cmd = new NpgsqlCommand(query, connection);
            cmd.Parameters.AddWithValue("@Id", idArchivo);
            
            return await cmd.ExecuteScalarAsync() as string;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error obteniendo ruta de archivo: {ex.Message}");
            return null;
        }
    }
    
    /// <summary>
    /// Obtiene el tipo MIME según la extensión
    /// </summary>
    private string ObtenerTipoMime(string extension)
    {
        return extension.ToLower() switch
        {
            ".pdf" => "application/pdf",
            ".png" => "image/png",
            ".jpg" => "image/jpeg",
            ".jpeg" => "image/jpeg",
            ".gif" => "image/gif",
            ".txt" => "text/plain",
            ".doc" => "application/msword",
            ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            ".xls" => "application/vnd.ms-excel",
            ".xlsx" => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            ".zip" => "application/zip",
            ".rar" => "application/x-rar-compressed",
            _ => "application/octet-stream"
        };
    }
    
    /// <summary>
    /// Elimina todos los archivos de un comercio
    /// </summary>
    public async Task<int> EliminarArchivosDeComercio(int idComercio)
    {
        try
        {
            var archivos = await ObtenerArchivosPorComercio(idComercio);
            int eliminados = 0;
            
            foreach (var archivo in archivos)
            {
                if (await EliminarArchivo(archivo.IdArchivo))
                {
                    eliminados++;
                }
            }
            
            return eliminados;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error eliminando archivos del comercio: {ex.Message}");
            return 0;
        }
    }
}