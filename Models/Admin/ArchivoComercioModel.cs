using System;

namespace Allva.Desktop.Models.Admin;

/// <summary>
/// Modelo para archivos asociados a comercios
/// Representa documentos, imágenes y otros archivos adjuntos
/// </summary>
public class ArchivoComercioModel
{
    // ============================================
    // PROPIEDADES BÁSICAS
    // ============================================
    
    public int IdArchivo { get; set; }
    public int IdComercio { get; set; }
    
    /// <summary>
    /// Nombre único del archivo en el servidor
    /// </summary>
    public string NombreArchivo { get; set; } = string.Empty;
    
    /// <summary>
    /// Nombre original del archivo subido por el usuario
    /// </summary>
    public string NombreOriginal { get; set; } = string.Empty;
    
    /// <summary>
    /// Tipo MIME del archivo (pdf, png, jpg, txt, etc)
    /// </summary>
    public string? TipoArchivo { get; set; }
    
    /// <summary>
    /// Tamaño del archivo en bytes
    /// </summary>
    public long? TamanoBytes { get; set; }
    
    /// <summary>
    /// Ruta completa del archivo en el servidor
    /// </summary>
    public string RutaArchivo { get; set; } = string.Empty;
    
    /// <summary>
    /// Descripción opcional del archivo
    /// </summary>
    public string? Descripcion { get; set; }
    
    /// <summary>
    /// Fecha y hora de subida del archivo
    /// </summary>
    public DateTime FechaSubida { get; set; } = DateTime.Now;
    
    /// <summary>
    /// Usuario que subió el archivo
    /// </summary>
    public string? SubidoPor { get; set; }
    
    /// <summary>
    /// Indica si el archivo está activo (no eliminado)
    /// </summary>
    public bool Activo { get; set; } = true;
    
    // ============================================
    // PROPIEDADES CALCULADAS PARA UI
    // ============================================
    
    /// <summary>
    /// Tamaño formateado para mostrar en UI (KB, MB)
    /// </summary>
    public string TamanoFormateado
    {
        get
        {
            if (!TamanoBytes.HasValue) return "N/A";
            
            if (TamanoBytes.Value < 1024)
                return $"{TamanoBytes.Value} B";
            else if (TamanoBytes.Value < 1024 * 1024)
                return $"{TamanoBytes.Value / 1024.0:F2} KB";
            else if (TamanoBytes.Value < 1024 * 1024 * 1024)
                return $"{TamanoBytes.Value / (1024.0 * 1024.0):F2} MB";
            else
                return $"{TamanoBytes.Value / (1024.0 * 1024.0 * 1024.0):F2} GB";
        }
    }
    
    /// <summary>
    /// Icono emoji según el tipo de archivo
    /// </summary>
    public string IconoArchivo
    {
        get
        {
            if (string.IsNullOrEmpty(TipoArchivo)) return "📎";
            
            var tipo = TipoArchivo.ToLower();
            
            if (tipo.Contains("pdf")) return "📄";
            if (tipo.Contains("image") || tipo.Contains("png") || tipo.Contains("jpg") || tipo.Contains("jpeg")) return "🖼️";
            if (tipo.Contains("text") || tipo.Contains("txt")) return "📝";
            if (tipo.Contains("word") || tipo.Contains("doc")) return "📃";
            if (tipo.Contains("excel") || tipo.Contains("xls")) return "📊";
            if (tipo.Contains("zip") || tipo.Contains("rar")) return "📦";
            
            return "📎";
        }
    }
    
    /// <summary>
    /// Fecha formateada para mostrar en UI
    /// </summary>
    public string FechaFormateada => FechaSubida.ToString("dd/MM/yyyy HH:mm");
    
    /// <summary>
    /// Información completa del archivo para tooltip
    /// </summary>
    public string InformacionCompleta => 
        $"{NombreOriginal}\n" +
        $"Tamaño: {TamanoFormateado}\n" +
        $"Tipo: {TipoArchivo}\n" +
        $"Subido: {FechaFormateada}\n" +
        $"Por: {SubidoPor ?? "Desconocido"}";
}