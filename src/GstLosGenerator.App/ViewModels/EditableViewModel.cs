using System.Runtime.CompilerServices;
using CommunityToolkit.Mvvm.ComponentModel;

namespace GstLosGenerator.App.ViewModels;

// Base de los modelos de vista que editan el proyecto: cada cambio pasa por la sesión para poder deshacerlo.
public abstract class EditableViewModel(Session session) : ObservableObject
{
    protected Session Session { get; } = session;

    // Cambia un dato del modelo si es distinto: foto para deshacer, cambio y aviso a la vista.
    protected bool Edit<T>(T current, T value, Action<T> set, [CallerMemberName] string? property = null)
    {
        if (EqualityComparer<T>.Default.Equals(current, value))
        {
            return false;
        }
        Session.BeforeChange(GetHashCode() + ":" + property);
        set(value);
        OnPropertyChanged(property);
        Session.AfterChange();
        return true;
    }
}
