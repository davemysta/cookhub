using MudBlazor;

namespace CookHub.Components.Layout;

public static class AppTheme
{
    public static MudTheme Theme { get; } = new()
    {
        PaletteLight = new PaletteLight
        {
            Primary = "#C2410C",
            Secondary = "#15803D",
            Tertiary = "#B45309",
            AppbarBackground = "#C2410C",
            Background = "#FAFAF9",
            DrawerBackground = "#FFFFFF",
        },
        PaletteDark = new PaletteDark
        {
            Primary = "#FB923C",
            Secondary = "#4ADE80",
            Tertiary = "#FBBF24",
        },
        LayoutProperties = new LayoutProperties
        {
            DefaultBorderRadius = "8px",
        },
    };
}
