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
            
            var query = @"SELECT id_archivo, id_comercio, nombre_archivo, ruta_archivo,
                                 tipo_archivo, tamano_kb, descripcion, fecha_subida,
                                 subido_por, activo
                          FROM archivos_comercios 
                          WHERE id_comercio = @IdComercio AND (activo IS NULL OR activo = true)
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
                    RutaArchivo = reader.GetString(3),
                    TipoArchivo = reader.GetString(4),
                    TamanoKb = reader.IsDBNull(5) ? null : reader.GetInt32(5),
                    Descripcion = reader.IsDBNull(6) ? null : reader.GetString(6),
                    FechaSubida = reader.IsDBNull(7) ? null : reader.GetDateTime(7),
                    SubidoPor = reader.IsDBNull(8) ? null : reader.GetInt32(8),
                    Activo = reader.IsDBNull(9) ? null : reader.GetBoolean(9)
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
            
            // Calcular tamaño en KB
            var tamanoBytes = new FileInfo(rutaDestino).Length;
            var tamanoKb = (int)(tamanoBytes / 1024);
            if (tamanoKb == 0 && tamanoBytes > 0) tamanoKb = 1; // Mínimo 1 KB
            
            // Determinar tipo de archivo (texto descriptivo, no MIME)
            var tipoArchivo = ObtenerTipoArchivo(extension);
            
            // Guardar en BD
            using var connection = new NpgsqlConnection(ConnectionString);
            await connection.OpenAsync();
            
            var query = @"INSERT INTO archivos_comercios 
                          (id_comercio, nombre_archivo, ruta_archivo, tipo_archivo, 
                           tamano_kb, descripcion, fecha_subida, activo)
                          VALUES (@IdComercio, @NombreArchivo, @Ruta, @TipoArchivo,
                                  @TamanoKb, @Descripcion, @FechaSubida, @Activo)
                          RETURNING id_archivo";
            
            using var cmd = new NpgsqlCommand(query, connection);
            cmd.Parameters.AddWithValue("@IdComercio", idComercio);
            cmd.Parameters.AddWithValue("@NombreArchivo", nombreUnico);
            cmd.Parameters.AddWithValue("@Ruta", rutaDestino);
            cmd.Parameters.AddWithValue("@TipoArchivo", tipoArchivo);
            cmd.Parameters.AddWithValue("@TamanoKb", (object?)tamanoKb ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@Descripcion", (object?)descripcion ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@FechaSubida", DateTime.Now);
            cmd.Parameters.AddWithValue("@Activo", true);
            
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
    /// Descarga un archivo a una ubicación específica
    /// </summary>
    public async Task DescargarArchivo(int idArchivo, string rutaDestino)
    {
        try
        {
            var rutaOrigen = await ObtenerRutaArchivo(idArchivo);
            
            if (string.IsNullOrEmpty(rutaOrigen))
                throw new FileNotFoundException("Archivo no encontrado en la base de datos");
            
            if (!File.Exists(rutaOrigen))
                throw new FileNotFoundException("Archivo físico no encontrado", rutaOrigen);
            
            // Crear directorio de destino si no existe
            var directorioDestino = Path.GetDirectoryName(rutaDestino);
            if (!string.IsNullOrEmpty(directorioDestino))
                Directory.CreateDirectory(directorioDestino);
            
            // Copiar archivo
            File.Copy(rutaOrigen, rutaDestino, overwrite: true);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error descargando archivo: {ex.Message}");
            throw;
        }
    }
    
    /// <summary>
    /// Obtiene la ruta de un archivo (devuelve la ruta para abrir)
    /// </summary>
    public async Task<string?> ObtenerRutaArchivo(int idArchivo)
    {
        try
        {
            using var connection = new NpgsqlConnection(ConnectionString);
            await connection.OpenAsync();
            
            var query = "SELECT ruta_archivo FROM archivos_comercios WHERE id_archivo = @Id AND (activo IS NULL OR activo = true)";
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
    /// Obtiene el tipo de archivo según la extensión (texto descriptivo)
    /// </summary>
    private string ObtenerTipoArchivo(string extension)
    {
        return extension.ToLower() switch
        {
            ".pdf" => "PDF",
            ".png" => "Imagen PNG",
            ".jpg" => "Imagen JPEG",
            ".jpeg" => "Imagen JPEG",
            ".gif" => "Imagen GIF",
            ".bmp" => "Imagen BMP",
            ".txt" => "Texto",
            ".doc" => "Documento Word",
            ".docx" => "Documento Word",
            ".xls" => "Hoja de Cálculo",
            ".xlsx" => "Hoja de Cálculo",
            ".zip" => "Archivo ZIP",
            ".rar" => "Archivo RAR",
            _ => "Archivo"
        };
    }
    
    /// <summary>
    /// Obtiene el tipo MIME según la extensión (mantener por compatibilidad)
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