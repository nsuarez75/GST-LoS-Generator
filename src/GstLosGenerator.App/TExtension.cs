// {l:T 'Texto'} en las vistas: el texto en castellano traducido al idioma de la interfaz (ver L.cs).
// Al cambiar de idioma la ventana se vuelve a crear, así que basta con traducir al cargarla.
using Avalonia.Markup.Xaml;
using GstLosGenerator.Core;

namespace GstLosGenerator.App;

public sealed class TExtension(string text) : MarkupExtension
{
    public override object ProvideValue(IServiceProvider serviceProvider) => L.T(text);
}
