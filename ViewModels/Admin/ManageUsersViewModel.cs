using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Allva.Desktop.Models;
// using Allva.Desktop.Services; // Comentado temporalmente

namespace Allva.Desktop.ViewModels.Admin;

/// <summary>
/// ViewModel para la gestión de usuarios normales y flotantes
/// </summary>
public partial class ManageUsersViewModel : ObservableObject
{
    // private readonly DatabaseService _databaseService; // Comentado temporalmente
    
    // ============================================
    // PROPIEDADES OBSERVABLES
    // ============================================
    
    [ObservableProperty]
    private ObservableCollection<UserModel> _usuarios = new();
    
    [ObservableProperty]
    private bool _mostrarFormulario;
    
    [ObservableProperty]
    private bool _mostrarMensaje;
    
    [ObservableProperty]
    private string _mensaje = string.Empty;
    
    [ObservableProperty]
    private string _tituloFormulario = "Crear Usuario";
    
    [ObservableProperty]
    private bool _modoEdicion;
    
    // ============================================
    // CAMPOS DEL FORMULARIO
    // ============================================
    
    [ObservableProperty]
    private string _formNumeroUsuario = string.Empty;
    
    [ObservableProperty]
    private string _formNombre = string.Empty;
    
    [ObservableProperty]
    private string _formApellidos = string.Empty;
    
    [ObservableProperty]
    private string _formCorreo = string.Empty;
    
    [ObservableProperty]
    private string _formTelefono = string.Empty;
    
    [ObservableProperty]
    private string _formPassword = string.Empty;
    
    [ObservableProperty]
    private string _formObservaciones = string.Empty;
    
    [ObservableProperty]
    private bool _formEsFlotante;
    
    [ObservableProperty]
    private bool _formActivo = true;
    
    // ============================================
    // BÚSQUEDA Y ASIGNACIÓN DE LOCALES
    // ============================================
    
    [ObservableProperty]
    private string _busquedaLocal = string.Empty;
    
    [ObservableProperty]
    private bool _mostrarResultadosBusqueda;
    
    [ObservableProperty]
    private ObservableCollection<LocalFormModel> _resultadosBusquedaLocales = new();
    
    [ObservableProperty]
    private ObservableCollection<LocalFormModel> _localesAsignados = new();
    
    // ============================================
    // ESTADÍSTICAS
    // ============================================
    
    [ObservableProperty]
    private int _totalUsuarios;
    
    [ObservableProperty]
    private int _usuariosActivos;
    
    [ObservableProperty]
    private int _usuariosFlotantes;
    
    // Usuario en edición
    private UserModel? _usuarioEnEdicion;
    
    // ============================================
    // CONSTRUCTOR
    // ============================================
    
    public ManageUsersViewModel()
    {
        // _databaseService = new DatabaseService(); // Comentado temporalmente
        _ = CargarUsuariosAsync();
    }
    
    // ============================================
    // CARGA DE DATOS - TEMPORALMENTE CON DATOS DE PRUEBA
    // ============================================
    
    private async Task CargarUsuariosAsync()
    {
        try
        {
            // TODO: Reemplazar con llamada real a base de datos
            await Task.Delay(100); // Simular carga
            
            // Datos de prueba
            Usuarios.Clear();
            Usuarios.Add(new UserModel
            {
                IdUsuario = 1,
                NumeroUsuario = "USR001",
                Nombre = "Juan",
                Apellidos = "Pérez",
                Correo = "juan.perez@allva.com",
                Telefono = "+54 11 1234-5678",
                EsFlotante = false,
                Activo = true,
                NombreLocal = "Sucursal Centro",
                NombreComercio = "Allva Travel",
                UltimoAcceso = DateTime.Now.AddHours(-2)
            });
            
            Usuarios.Add(new UserModel
            {
                IdUsuario = 2,
                NumeroUsuario = "USR002",
                Nombre = "María",
                Apellidos = "González",
                Correo = "maria.gonzalez@allva.com",
                Telefono = "+54 11 8765-4321",
                EsFlotante = true,
                Activo = true,
                NombreLocal = "Varios Locales",
                NombreComercio = "Allva Travel",
                UltimoAcceso = DateTime.Now.AddDays(-1)
            });
            
            ActualizarEstadisticas();
        }
        catch (Exception ex)
        {
            MostrarMensajeTemporalmente($"Error al cargar usuarios: {ex.Message}");
        }
    }
    
    private void ActualizarEstadisticas()
    {
        TotalUsuarios = Usuarios.Count;
        UsuariosActivos = Usuarios.Count(u => u.Activo);
        UsuariosFlotantes = Usuarios.Count(u => u.EsFlotante);
    }
    
    // ============================================
    // COMANDOS DE FORMULARIO
    // ============================================
    
    [RelayCommand]
    private void MostrarFormularioCrear()
    {
        LimpiarFormulario();
        TituloFormulario = "Crear Usuario";
        ModoEdicion = false;
        MostrarFormulario = true;
    }
    
    [RelayCommand]
    private void CerrarFormulario()
    {
        MostrarFormulario = false;
        LimpiarFormulario();
    }
    
    [RelayCommand]
    private async Task GuardarUsuarioAsync()
    {
        try
        {
            // Validaciones
            if (string.IsNullOrWhiteSpace(FormNumeroUsuario) ||
                string.IsNullOrWhiteSpace(FormNombre) ||
                string.IsNullOrWhiteSpace(FormApellidos) ||
                string.IsNullOrWhiteSpace(FormCorreo))
            {
                MostrarMensajeTemporalmente("Por favor completa todos los campos obligatorios (*)");
                return;
            }
            
            if (!ModoEdicion && string.IsNullOrWhiteSpace(FormPassword))
            {
                MostrarMensajeTemporalmente("La contraseña es obligatoria para nuevos usuarios");
                return;
            }
            
            if (LocalesAsignados.Count == 0)
            {
                MostrarMensajeTemporalmente("Debes asignar al menos un local al usuario");
                return;
            }
            
            // TODO: Implementar guardado real en base de datos
            await Task.Delay(500); // Simular guardado
            
            MostrarMensajeTemporalmente(
                ModoEdicion 
                    ? "Usuario actualizado correctamente" 
                    : "Usuario creado correctamente"
            );
            
            await CargarUsuariosAsync();
            CerrarFormulario();
        }
        catch (Exception ex)
        {
            MostrarMensajeTemporalmente($"Error al guardar usuario: {ex.Message}");
        }
    }
    
    // ============================================
    // COMANDOS DE GESTIÓN DE USUARIOS
    // ============================================
    
    [RelayCommand]
    private async Task EditarUsuarioAsync(UserModel usuario)
    {
        try
        {
            _usuarioEnEdicion = usuario;
            
            FormNumeroUsuario = usuario.NumeroUsuario;
            FormNombre = usuario.Nombre;
            FormApellidos = usuario.Apellidos;
            FormCorreo = usuario.Correo;
            FormTelefono = usuario.Telefono ?? string.Empty;
            FormObservaciones = usuario.Observaciones ?? string.Empty;
            FormEsFlotante = usuario.EsFlotante;
            FormActivo = usuario.Activo;
            FormPassword = string.Empty;
            
            // TODO: Cargar locales reales de base de datos
            await Task.Delay(100);
            
            TituloFormulario = "Editar Usuario";
            ModoEdicion = true;
            MostrarFormulario = true;
        }
        catch (Exception ex)
        {
            MostrarMensajeTemporalmente($"Error al cargar datos del usuario: {ex.Message}");
        }
    }
    
    [RelayCommand]
    private async Task CambiarEstadoUsuarioAsync(UserModel usuario)
    {
        try
        {
            usuario.Activo = !usuario.Activo;
            
            // TODO: Actualizar en base de datos
            await Task.Delay(100);
            
            MostrarMensajeTemporalmente(
                usuario.Activo 
                    ? $"Usuario {usuario.NombreCompleto} activado" 
                    : $"Usuario {usuario.NombreCompleto} desactivado"
            );
            
            ActualizarEstadisticas();
        }
        catch (Exception ex)
        {
            MostrarMensajeTemporalmente($"Error al cambiar estado: {ex.Message}");
        }
    }
    
    [RelayCommand]
    private async Task EliminarUsuarioAsync(UserModel usuario)
    {
        try
        {
            // TODO: Mostrar diálogo de confirmación
            // TODO: Eliminar de base de datos
            await Task.Delay(100);
            
            Usuarios.Remove(usuario);
            
            MostrarMensajeTemporalmente($"Usuario {usuario.NombreCompleto} eliminado");
            
            ActualizarEstadisticas();
        }
        catch (Exception ex)
        {
            MostrarMensajeTemporalmente($"Error al eliminar usuario: {ex.Message}");
        }
    }
    
    // ============================================
    // BÚSQUEDA Y ASIGNACIÓN DE LOCALES
    // ============================================
    
    [RelayCommand]
    private async Task BuscarLocalAsync()
    {
        try
        {
            if (string.IsNullOrWhiteSpace(BusquedaLocal))
            {
                MostrarResultadosBusqueda = false;
                return;
            }
            
            // TODO: Buscar locales reales en base de datos
            await Task.Delay(100);
            
            // Datos de prueba
            ResultadosBusquedaLocales.Clear();
            ResultadosBusquedaLocales.Add(new LocalFormModel
            {
                IdLocal = 1,
                CodigoLocal = "LOC001",
                NombreLocal = "Sucursal Centro",
                Direccion = "Av. Principal 123"
            });
            
            MostrarResultadosBusqueda = ResultadosBusquedaLocales.Count > 0;
        }
        catch (Exception ex)
        {
            MostrarMensajeTemporalmente($"Error al buscar locales: {ex.Message}");
        }
    }
    
    [RelayCommand]
    private void SeleccionarLocal(LocalFormModel local)
    {
        if (!LocalesAsignados.Any(l => l.IdLocal == local.IdLocal))
        {
            LocalesAsignados.Add(local);
        }
        
        BusquedaLocal = string.Empty;
        MostrarResultadosBusqueda = false;
        ResultadosBusquedaLocales.Clear();
    }
    
    [RelayCommand]
    private void QuitarLocalAsignado(LocalFormModel local)
    {
        LocalesAsignados.Remove(local);
    }
    
    // ============================================
    // MÉTODOS AUXILIARES
    // ============================================
    
    private void LimpiarFormulario()
    {
        FormNumeroUsuario = string.Empty;
        FormNombre = string.Empty;
        FormApellidos = string.Empty;
        FormCorreo = string.Empty;
        FormTelefono = string.Empty;
        FormPassword = string.Empty;
        FormObservaciones = string.Empty;
        FormEsFlotante = false;
        FormActivo = true;
        
        LocalesAsignados.Clear();
        ResultadosBusquedaLocales.Clear();
        BusquedaLocal = string.Empty;
        MostrarResultadosBusqueda = false;
        
        _usuarioEnEdicion = null;
    }
    
    private async void MostrarMensajeTemporalmente(string mensaje)
    {
        Mensaje = mensaje;
        MostrarMensaje = true;
        
        await Task.Delay(3000);
        
        MostrarMensaje = false;
    }
}