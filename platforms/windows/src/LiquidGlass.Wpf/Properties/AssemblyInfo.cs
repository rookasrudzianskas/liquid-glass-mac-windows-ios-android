using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Markup;

[assembly: ThemeInfo(ResourceDictionaryLocation.None, ResourceDictionaryLocation.SourceAssembly)]

[assembly: XmlnsDefinition("https://liquid-glass-engineering.vercel.app/wpf", "LiquidGlass.Wpf")]
[assembly: XmlnsDefinition("https://liquid-glass-engineering.vercel.app/wpf", "LiquidGlass")]
[assembly: XmlnsPrefix("https://liquid-glass-engineering.vercel.app/wpf", "lg")]

[assembly: InternalsVisibleTo("LiquidGlass.Wpf.Tests")]
