using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Allva.Desktop.Models.Admin;
using Allva.Desktop.Models;
using Allva.Desktop.Services;
using Npgsql;

namespace Allva.Desktop.ViewModels.Admin;

/// <summary>
/// ViewModel para la gestión de comercios en el panel de administración
/// VERSIÓN COMPLETA Y MEJORADA con todas las funcionalidades
/// </summary>
public partial class ManageComerciosViewModel : ObservableObject
{
    // ============================================
    // CONFIGURACIÓN DE BASE DE DATOS
    // ============================================
    
    private const string ConnectionString = "Host=switchyard.proxy.rlwy.net;Port=55839;Database=railway;Username=postgres;Password=ysTQxChOYSWUuAPzmYQokqrjpYnKSGbk;";

    // ============================================
    // PROPIEDADES OBSERVABLES - DATOS PRINCIPALES
    // ============================================

    [ObservableProperty]
    private ObservableCollection<ComercioModel> _comercios = new();

    [ObservableProperty]
    private ObservableCollection<ComercioModel> _comerciosFiltrados = new();

    [ObservableProperty]
    private ComercioModel? _comercioSeleccionado;

    [ObservableProperty]
    private bool _cargando;

    [ObservableProperty]
    private bool _mostrarMensajeExito;

    [ObservableProperty]
    private string _mensajeExito = string.Empty;

    // ============================================
    // PROPIEDADES PARA PANEL DERECHO
    // ============================================

    [ObservableProperty]
    private bool _mostrarPanelDerecho = false;

    [ObservableProperty]
    private string _tituloPanelDerecho = "Detalles del Comercio";

    [ObservableProperty]
    private object? _contenidoPanelDerecho;

    [ObservableProperty]
    private bool _esModoCreacion = false;

    // ============================================
    // PROPIEDADES PARA FORMULARIO
    // ============================================

    [ObservableProperty]
    private bool _mostrarFormulario;

    [ObservableProperty]
    private bool _modoEdicion;

    [ObservableProperty]
    private string _tituloFormulario = "Crear Comercio";

    // Campos del formulario de comercio
    [ObservableProperty]
    private string _formNombreComercio = string.Empty;

    [ObservableProperty]
    private string _formNombreSrl = string.Empty;

    [ObservableProperty]
    private string _formDireccionCentral = string.Empty;

    [ObservableProperty]
    private string _formNumeroContacto = string.Empty;

    [ObservableProperty]
    private string _formMailContacto = string.Empty;

    [ObservableProperty]
    private string _formPais = string.Empty;

    [ObservableProperty]
    private string _formObservaciones = string.Empty;

    [ObservableProperty]
    private decimal _formPorcentajeComisionDivisas = 0;

    [ObservableProperty]
    private bool _formActivo = true;

    // Locales del comercio
    [ObservableProperty]
    private ObservableCollection<LocalFormModel> _localesComercio = new();

    // ============================================
    // PROPIEDADES PARA FILTROS
    // ============================================

    [ObservableProperty]
    private string _filtroBusqueda = string.Empty;

    [ObservableProperty]
    private string _filtroPais = "Todos";

    [ObservableProperty]
    private string _filtroEstado = "Todos";

    [ObservableProperty]
    private ObservableCollection<string> _paisesDisponibles = new();

    // ============================================
    // PROPIEDADES PARA ARCHIVOS
    // ============================================

    [ObservableProperty]
    private ObservableCollection<ArchivoComercioModel> _archivosComercioSeleccionado = new();

    // ============================================
    // SERVICIOS
    // ============================================

    private readonly ArchivoService _archivoService = new();

    // ============================================
    // PROPIEDADES CALCULADAS
    // ============================================

    public int TotalComercios => Comercios.Count;
    public int ComerciosActivos => Comercios.Count(c => c.Activo);
    public int ComerciosInactivos => Comercios.Count(c => !c.Activo);
    public int TotalLocales => Comercios.Sum(c => c.CantidadLocales);

    // ============================================
    // CONSTRUCTOR
    // ============================================

    public ManageComerciosViewModel()
    {
        // Cargar datos desde la base de datos
        _ = CargarDatosDesdeBaseDatos();
    }

    // ============================================
    // MÉTODOS DE BASE DE DATOS - CARGAR
    // ============================================

    private async Task CargarDatosDesdeBaseDatos()
    {
        Cargando = true;
        
        try
        {
            using var connection = new NpgsqlConnection(ConnectionString);
            await connection.OpenAsync();

            var comercios = await CargarComercios(connection);
            
            Comercios.Clear();
            foreach (var comercio in comercios)
            {
                // Cargar locales del comercio CON PERMISOS
                comercio.Locales = await CargarLocalesDelComercio(connection, comercio.IdComercio);
                
                // Contar usuarios
                comercio.TotalUsuarios = await ContarUsuariosDelComercio(connection, comercio.IdComercio);
                
                Comercios.Add(comercio);
            }

            // Actualizar contadores
            OnPropertyChanged(nameof(TotalComercios));
            OnPropertyChanged(nameof(ComerciosActivos));
            OnPropertyChanged(nameof(ComerciosInactivos));
            OnPropertyChanged(nameof(TotalLocales));
            
            // Inicializar filtros
            await InicializarFiltros();
        }
        catch (Exception ex)
        {
            MensajeExito = $"Error al cargar datos: {ex.Message}";
            MostrarMensajeExito = true;
            await Task.Delay(3000);
            MostrarMensajeExito = false;
        }
        finally
        {
            Cargando = false;
        }
    }

    private async Task<List<ComercioModel>> CargarComercios(NpgsqlConnection connection)
    {
        var comercios = new List<ComercioModel>();
        
        var query = @"SELECT id_comercio, nombre_comercio, nombre_srl, direccion_central,
                             numero_contacto, mail_contacto, pais, observaciones,
                             porcentaje_comision_divisas, activo, fecha_registro,
                             fecha_ultima_modificacion
                      FROM comercios 
                      ORDER BY nombre_comercio";
        
        using var cmd = new NpgsqlCommand(query, connection);
        using var reader = await cmd.ExecuteReaderAsync();
        
        while (await reader.ReadAsync())
        {
            comercios.Add(new ComercioModel
            {
                IdComercio = reader.GetInt32(0),
                NombreComercio = reader.GetString(1),
                NombreSrl = reader.IsDBNull(2) ? string.Empty : reader.GetString(2),
                DireccionCentral = reader.GetString(3),
                NumeroContacto = reader.IsDBNull(4) ? string.Empty : reader.GetString(4),
                MailContacto = reader.GetString(5),
                Pais = reader.GetString(6),
                Observaciones = reader.IsDBNull(7) ? null : reader.GetString(7),
                PorcentajeComisionDivisas = reader.GetDecimal(8),
                Activo = reader.GetBoolean(9),
                FechaRegistro = reader.GetDateTime(10),
                FechaUltimaModificacion = reader.GetDateTime(11)
            });
        }
        
        return comercios;
    }

    private async Task<List<LocalSimpleModel>> CargarLocalesDelComercio(NpgsqlConnection connection, int idComercio)
    {
        var locales = new List<LocalSimpleModel>();
        
        var query = @"SELECT id_local, codigo_local, nombre_local, direccion, local_numero,
                             escalera, piso, telefono, email, observaciones,
                             numero_usuarios_max, activo,
                             modulo_divisas, modulo_pack_alimentos, 
                             modulo_billetes_avion, modulo_pack_viajes
                      FROM locales 
                      WHERE id_comercio = @IdComercio
                      ORDER BY nombre_local";
        
        using var cmd = new NpgsqlCommand(query, connection);
        cmd.Parameters.AddWithValue("@IdComercio", idComercio);
        
        using var reader = await cmd.ExecuteReaderAsync();
        
        while (await reader.ReadAsync())
        {
            locales.Add(new LocalSimpleModel
            {
                IdLocal = reader.GetInt32(0),
                CodigoLocal = reader.GetString(1),
                NombreLocal = reader.GetString(2),
                Direccion = reader.GetString(3),
                LocalNumero = reader.IsDBNull(4) ? string.Empty : reader.GetString(4),
                Escalera = reader.IsDBNull(5) ? null : reader.GetString(5),
                Piso = reader.IsDBNull(6) ? null : reader.GetString(6),
                Telefono = reader.IsDBNull(7) ? null : reader.GetString(7),
                Email = reader.IsDBNull(8) ? null : reader.GetString(8),
                Observaciones = reader.IsDBNull(9) ? null : reader.GetString(9),
                NumeroUsuariosMax = reader.GetInt32(10),
                Activo = reader.GetBoolean(11),
                ModuloDivisas = reader.GetBoolean(12),
                ModuloPackAlimentos = reader.GetBoolean(13),
                ModuloBilletesAvion = reader.GetBoolean(14),
                ModuloPackViajes = reader.GetBoolean(15)
            });
        }
        
        return locales;
    }

    private async Task<int> ContarUsuariosDelComercio(NpgsqlConnection connection, int idComercio)
    {
        var query = @"SELECT COUNT(*) 
                      FROM usuarios u
                      INNER JOIN permisos_locales_usuarios plu ON u.id_usuario = plu.id_usuario
                      INNER JOIN locales l ON plu.id_local = l.id_local
                      WHERE l.id_comercio = @IdComercio AND u.activo = true";
        
        using var cmd = new NpgsqlCommand(query, connection);
        cmd.Parameters.AddWithValue("@IdComercio", idComercio);
        
        var result = await cmd.ExecuteScalarAsync();
        return result != null ? Convert.ToInt32(result) : 0;
    }

    // ============================================
    // COMANDOS - PANEL DERECHO
    // ============================================

    [RelayCommand]
    private void MostrarFormularioComercio()
    {
        LimpiarFormulario();
        EsModoCreacion = true;
        ModoEdicion = false;
        TituloFormulario = "Crear Nuevo Comercio";
        TituloPanelDerecho = "Crear Nuevo Comercio";
        MostrarFormulario = true;
        MostrarPanelDerecho = true;
    }

    [RelayCommand]
    private void EditarComercio(ComercioModel comercio)
    {
        ComercioSeleccionado = comercio;
        CargarDatosEnFormulario(comercio);
        EsModoCreacion = false;
        ModoEdicion = true;
        TituloFormulario = "Editar Comercio";
        TituloPanelDerecho = $"Editar: {comercio.NombreComercio}";
        MostrarFormulario = true;
        MostrarPanelDerecho = true;
    }

    [RelayCommand]
    private void VerDetallesComercio(ComercioModel comercio)
    {
        ComercioSeleccionado = comercio;
        TituloPanelDerecho = $"Detalles de {comercio.NombreComercio}";
        MostrarFormulario = false;
        MostrarPanelDerecho = true;
        
        // Cargar archivos del comercio
        _ = CargarArchivosComercio(comercio.IdComercio);
    }

    [RelayCommand]
    private void CerrarPanelDerecho()
    {
        MostrarPanelDerecho = false;
        MostrarFormulario = false;
        ContenidoPanelDerecho = null;
        ComercioSeleccionado = null;
        LimpiarFormulario();
    }

    // ============================================
    // COMANDOS - ACCIONES CRUD
    // ============================================

    [RelayCommand]
    private async Task GuardarComercio()
    {
        // Validar campos
        if (string.IsNullOrWhiteSpace(FormNombreComercio))
        {
            MensajeExito = "El nombre del comercio es obligatorio";
            MostrarMensajeExito = true;
            await Task.Delay(3000);
            MostrarMensajeExito = false;
            return;
        }

        if (string.IsNullOrWhiteSpace(FormMailContacto))
        {
            MensajeExito = "El email de contacto es obligatorio";
            MostrarMensajeExito = true;
            await Task.Delay(3000);
            MostrarMensajeExito = false;
            return;
        }

        Cargando = true;

        try
        {
            if (ModoEdicion && ComercioSeleccionado != null)
            {
                await ActualizarComercio();
            }
            else
            {
                await CrearNuevoComercio();
            }

            // Recargar datos
            await CargarDatosDesdeBaseDatos();

            // Cerrar panel
            CerrarPanelDerecho();

            // Mostrar mensaje de éxito
            MensajeExito = ModoEdicion ? "Comercio actualizado correctamente" : "Comercio creado exitosamente";
            MostrarMensajeExito = true;
            await Task.Delay(3000);
            MostrarMensajeExito = false;
        }
        catch (Exception ex)
        {
            MensajeExito = $"Error al guardar: {ex.Message}";
            MostrarMensajeExito = true;
            await Task.Delay(5000);
            MostrarMensajeExito = false;
        }
        finally
        {
            Cargando = false;
        }
    }

    private async Task CrearNuevoComercio()
    {
        using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync();
        using var transaction = await connection.BeginTransactionAsync();

        try
        {
            // Insertar comercio
            var queryComercio = @"INSERT INTO comercios 
                                  (nombre_comercio, nombre_srl, direccion_central, numero_contacto,
                                   mail_contacto, pais, observaciones, porcentaje_comision_divisas, activo)
                                  VALUES (@Nombre, @Srl, @Direccion, @Telefono, @Email, @Pais, 
                                          @Observaciones, @Comision, @Activo)
                                  RETURNING id_comercio";

            using var cmdComercio = new NpgsqlCommand(queryComercio, connection, transaction);
            cmdComercio.Parameters.AddWithValue("@Nombre", FormNombreComercio);
            cmdComercio.Parameters.AddWithValue("@Srl", FormNombreSrl);
            cmdComercio.Parameters.AddWithValue("@Direccion", FormDireccionCentral);
            cmdComercio.Parameters.AddWithValue("@Telefono", FormNumeroContacto);
            cmdComercio.Parameters.AddWithValue("@Email", FormMailContacto);
            cmdComercio.Parameters.AddWithValue("@Pais", FormPais);
            cmdComercio.Parameters.AddWithValue("@Observaciones", (object?)FormObservaciones ?? DBNull.Value);
            cmdComercio.Parameters.AddWithValue("@Comision", FormPorcentajeComisionDivisas);
            cmdComercio.Parameters.AddWithValue("@Activo", FormActivo);

            var idComercio = (int)(await cmdComercio.ExecuteScalarAsync())!;

            // Insertar locales
            foreach (var local in LocalesComercio)
            {
                await InsertarLocal(connection, transaction, idComercio, local);
            }

            await transaction.CommitAsync();
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    private async Task ActualizarComercio()
    {
        if (ComercioSeleccionado == null) return;

        using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync();

        var query = @"UPDATE comercios 
                      SET nombre_comercio = @Nombre,
                          nombre_srl = @Srl,
                          direccion_central = @Direccion,
                          numero_contacto = @Telefono,
                          mail_contacto = @Email,
                          pais = @Pais,
                          observaciones = @Observaciones,
                          porcentaje_comision_divisas = @Comision,
                          activo = @Activo,
                          fecha_ultima_modificacion = CURRENT_TIMESTAMP
                      WHERE id_comercio = @Id";

        using var cmd = new NpgsqlCommand(query, connection);
        cmd.Parameters.AddWithValue("@Nombre", FormNombreComercio);
        cmd.Parameters.AddWithValue("@Srl", FormNombreSrl);
        cmd.Parameters.AddWithValue("@Direccion", FormDireccionCentral);
        cmd.Parameters.AddWithValue("@Telefono", FormNumeroContacto);
        cmd.Parameters.AddWithValue("@Email", FormMailContacto);
        cmd.Parameters.AddWithValue("@Pais", FormPais);
        cmd.Parameters.AddWithValue("@Observaciones", (object?)FormObservaciones ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@Comision", FormPorcentajeComisionDivisas);
        cmd.Parameters.AddWithValue("@Activo", FormActivo);
        cmd.Parameters.AddWithValue("@Id", ComercioSeleccionado.IdComercio);

        await cmd.ExecuteNonQueryAsync();

        // TODO: Actualizar locales si es necesario
    }

    [RelayCommand]
    private async Task EliminarComercio(ComercioModel comercio)
    {
        // TODO: Implementar confirmación con modal
        // Por ahora, implementación directa
        
        Cargando = true;

        try
        {
            using var connection = new NpgsqlConnection(ConnectionString);
            await connection.OpenAsync();

            // Eliminar archivos primero
            await _archivoService.EliminarArchivosDeComercio(comercio.IdComercio);

            // Eliminar comercio (CASCADE eliminará locales y relaciones)
            var query = "DELETE FROM comercios WHERE id_comercio = @Id";
            using var cmd = new NpgsqlCommand(query, connection);
            cmd.Parameters.AddWithValue("@Id", comercio.IdComercio);
            await cmd.ExecuteNonQueryAsync();

            // Recargar datos
            await CargarDatosDesdeBaseDatos();

            MensajeExito = "Comercio eliminado correctamente";
            MostrarMensajeExito = true;
            await Task.Delay(3000);
            MostrarMensajeExito = false;
        }
        catch (Exception ex)
        {
            MensajeExito = $"Error al eliminar: {ex.Message}";
            MostrarMensajeExito = true;
            await Task.Delay(5000);
            MostrarMensajeExito = false;
        }
        finally
        {
            Cargando = false;
        }
    }

    [RelayCommand]
    private async Task CambiarEstadoComercio(ComercioModel comercio)
    {
        try
        {
            using var connection = new NpgsqlConnection(ConnectionString);
            await connection.OpenAsync();
            
            var nuevoEstado = !comercio.Activo;
            var query = @"UPDATE comercios 
                          SET activo = @Estado, 
                              fecha_ultima_modificacion = CURRENT_TIMESTAMP
                          WHERE id_comercio = @Id";
            
            using var cmd = new NpgsqlCommand(query, connection);
            cmd.Parameters.AddWithValue("@Estado", nuevoEstado);
            cmd.Parameters.AddWithValue("@Id", comercio.IdComercio);
            
            await cmd.ExecuteNonQueryAsync();
            
            // Actualizar el modelo
            comercio.Activo = nuevoEstado;
            OnPropertyChanged(nameof(comercio.EstadoTexto));
            OnPropertyChanged(nameof(comercio.EstadoColor));
            OnPropertyChanged(nameof(comercio.EstadoBotonTexto));
            OnPropertyChanged(nameof(comercio.EstadoBotonColor));
            
            // Actualizar contadores
            OnPropertyChanged(nameof(ComerciosActivos));
            OnPropertyChanged(nameof(ComerciosInactivos));
            
            MensajeExito = $"Comercio {(nuevoEstado ? "activado" : "desactivado")} correctamente";
            MostrarMensajeExito = true;
            await Task.Delay(3000);
            MostrarMensajeExito = false;
        }
        catch (Exception ex)
        {
            MensajeExito = $"Error al cambiar estado: {ex.Message}";
            MostrarMensajeExito = true;
            await Task.Delay(5000);
            MostrarMensajeExito = false;
        }
    }

    // ============================================
    // COMANDOS - FILTROS
    // ============================================

    [RelayCommand]
    private void AplicarFiltros()
    {
        var filtrados = Comercios.AsEnumerable();
        
        // Filtro por búsqueda
        if (!string.IsNullOrWhiteSpace(FiltroBusqueda))
        {
            var busqueda = FiltroBusqueda.ToLower();
            filtrados = filtrados.Where(c => 
                c.NombreComercio.ToLower().Contains(busqueda) ||
                c.Locales.Any(l => l.NombreLocal.ToLower().Contains(busqueda) ||
                                   l.CodigoLocal.ToLower().Contains(busqueda))
            );
        }
        
        // Filtro por país
        if (!string.IsNullOrEmpty(FiltroPais) && FiltroPais != "Todos")
        {
            filtrados = filtrados.Where(c => c.Pais == FiltroPais);
        }
        
        // Filtro por estado
        if (!string.IsNullOrEmpty(FiltroEstado) && FiltroEstado != "Todos")
        {
            var activo = FiltroEstado == "Activo";
            filtrados = filtrados.Where(c => c.Activo == activo);
        }
        
        ComerciosFiltrados.Clear();
        foreach (var comercio in filtrados.OrderBy(c => c.NombreComercio))
        {
            ComerciosFiltrados.Add(comercio);
        }
    }

    // ============================================
    // COMANDOS - LOCALES
    // ============================================

    [RelayCommand]
    private void AgregarLocal()
    {
        var nuevoLocal = new LocalFormModel
        {
            CodigoLocal = GenerarCodigoLocal(),
            NombreLocal = $"Local {LocalesComercio.Count + 1}",
            Activo = true
        };
        
        LocalesComercio.Add(nuevoLocal);
    }

    [RelayCommand]
    private void EliminarLocal(LocalFormModel local)
    {
        LocalesComercio.Remove(local);
    }

    // ============================================
    // MÉTODOS AUXILIARES - FORMULARIO
    // ============================================

    private void LimpiarFormulario()
    {
        FormNombreComercio = string.Empty;
        FormNombreSrl = string.Empty;
        FormDireccionCentral = string.Empty;
        FormNumeroContacto = string.Empty;
        FormMailContacto = string.Empty;
        FormPais = string.Empty;
        FormObservaciones = string.Empty;
        FormPorcentajeComisionDivisas = 0;
        FormActivo = true;
        LocalesComercio.Clear();
    }

    private void CargarDatosEnFormulario(ComercioModel comercio)
    {
        FormNombreComercio = comercio.NombreComercio;
        FormNombreSrl = comercio.NombreSrl;
        FormDireccionCentral = comercio.DireccionCentral;
        FormNumeroContacto = comercio.NumeroContacto;
        FormMailContacto = comercio.MailContacto;
        FormPais = comercio.Pais;
        FormObservaciones = comercio.Observaciones ?? string.Empty;
        FormPorcentajeComisionDivisas = comercio.PorcentajeComisionDivisas;
        FormActivo = comercio.Activo;

        LocalesComercio.Clear();
        foreach (var local in comercio.Locales)
        {
            LocalesComercio.Add(new LocalFormModel
            {
                IdLocal = local.IdLocal,
                IdComercio = comercio.IdComercio,
                CodigoLocal = local.CodigoLocal,
                NombreLocal = local.NombreLocal,
                Direccion = local.Direccion,
                LocalNumero = local.LocalNumero,
                Escalera = local.Escalera,
                Piso = local.Piso,
                Telefono = local.Telefono,
                Email = local.Email,
                NumeroUsuariosMax = local.NumeroUsuariosMax,
                Observaciones = local.Observaciones,
                Activo = local.Activo,
                ModuloDivisas = local.ModuloDivisas,
                ModuloPackAlimentos = local.ModuloPackAlimentos,
                ModuloBilletesAvion = local.ModuloBilletesAvion,
                ModuloPackViajes = local.ModuloPackViajes
            });
        }
    }

    private string GenerarCodigoLocal()
    {
        // Generar código de 3 letras basado en el nombre del comercio + 4 dígitos aleatorios
        var letras = string.IsNullOrEmpty(FormNombreComercio) 
            ? "XXX" 
            : new string(FormNombreComercio.Where(char.IsLetter).Take(3).ToArray()).ToUpper().PadRight(3, 'X');
        
        var random = new Random();
        var digitos = string.Empty;
        
        // Generar 4 dígitos únicos
        var digitosUsados = new HashSet<int>();
        while (digitosUsados.Count < 4)
        {
            digitosUsados.Add(random.Next(0, 10));
        }
        
        digitos = string.Join("", digitosUsados);
        
        return $"{letras}{digitos}";
    }

    // ============================================
    // MÉTODOS AUXILIARES - LOCALES
    // ============================================

    private async Task InsertarLocal(NpgsqlConnection connection, NpgsqlTransaction transaction, 
                                      int idComercio, LocalFormModel local)
    {
        var query = @"INSERT INTO locales 
                      (id_comercio, codigo_local, nombre_local, direccion, local_numero, 
                       escalera, piso, telefono, email, observaciones, numero_usuarios_max, activo,
                       modulo_divisas, modulo_pack_alimentos, modulo_billetes_avion, modulo_pack_viajes)
                      VALUES (@IdComercio, @Codigo, @Nombre, @Direccion, @LocalNumero,
                              @Escalera, @Piso, @Telefono, @Email, @Observaciones, @MaxUsuarios, @Activo,
                              @Divisas, @Alimentos, @Billetes, @Viajes)";

        using var cmd = new NpgsqlCommand(query, connection, transaction);
        cmd.Parameters.AddWithValue("@IdComercio", idComercio);
        cmd.Parameters.AddWithValue("@Codigo", local.CodigoLocal);
        cmd.Parameters.AddWithValue("@Nombre", local.NombreLocal);
        cmd.Parameters.AddWithValue("@Direccion", local.Direccion);
        cmd.Parameters.AddWithValue("@LocalNumero", local.LocalNumero);
        cmd.Parameters.AddWithValue("@Escalera", (object?)local.Escalera ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@Piso", (object?)local.Piso ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@Telefono", (object?)local.Telefono ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@Email", (object?)local.Email ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@Observaciones", (object?)local.Observaciones ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@MaxUsuarios", local.NumeroUsuariosMax);
        cmd.Parameters.AddWithValue("@Activo", local.Activo);
        cmd.Parameters.AddWithValue("@Divisas", local.ModuloDivisas);
        cmd.Parameters.AddWithValue("@Alimentos", local.ModuloPackAlimentos);
        cmd.Parameters.AddWithValue("@Billetes", local.ModuloBilletesAvion);
        cmd.Parameters.AddWithValue("@Viajes", local.ModuloPackViajes);

        await cmd.ExecuteNonQueryAsync();
    }

    // ============================================
    // MÉTODOS AUXILIARES - ARCHIVOS
    // ============================================

    private async Task CargarArchivosComercio(int idComercio)
    {
        try
        {
            var archivos = await _archivoService.ObtenerArchivosPorComercio(idComercio);
            ArchivosComercioSeleccionado.Clear();
            foreach (var archivo in archivos)
            {
                ArchivosComercioSeleccionado.Add(archivo);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error cargando archivos: {ex.Message}");
        }
    }

    // ============================================
    // MÉTODOS AUXILIARES - FILTROS
    // ============================================

    private void CargarPaisesDisponibles()
    {
        PaisesDisponibles.Clear();
        PaisesDisponibles.Add("Todos");
        
        var paises = Comercios.Select(c => c.Pais).Distinct().OrderBy(p => p);
        foreach (var pais in paises)
        {
            if (!string.IsNullOrEmpty(pais))
            {
                PaisesDisponibles.Add(pais);
            }
        }
    }

    private async Task InicializarFiltros()
    {
        // Esperar un momento para que termine de cargar todo
        await Task.Delay(100);
        
        CargarPaisesDisponibles();
        
        // Inicializar con todos los comercios
        ComerciosFiltrados.Clear();
        foreach (var comercio in Comercios.OrderBy(c => c.NombreComercio))
        {
            ComerciosFiltrados.Add(comercio);
        }
    }
}