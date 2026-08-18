using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Markup;

namespace CivVIToolkit.App;

internal static class TrainerTemplateFactory
{
    private const string MainTemplateXaml = """
<DataTemplate xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation">
    <Grid MinHeight="56" Padding="8,5" ColumnSpacing="10">
        <Grid.ColumnDefinitions>
            <ColumnDefinition Width="*" />
            <ColumnDefinition Width="88" />
            <ColumnDefinition Width="220" />
            <ColumnDefinition Width="100" />
        </Grid.ColumnDefinitions>
        <StackPanel VerticalAlignment="Center" Spacing="1">
            <TextBlock Text="{Binding DisplayName}" FontWeight="SemiBold" TextTrimming="CharacterEllipsis" />
            <TextBlock Text="{Binding Scope}" FontSize="11" Foreground="{ThemeResource TextFillColorSecondaryBrush}" />
        </StackPanel>
        <Border Grid.Column="1" Width="88" Height="32" Background="{ThemeResource CardBackgroundFillColorSecondaryBrush}" CornerRadius="6" VerticalAlignment="Center">
            <TextBlock Text="{Binding Shortcut}" FontFamily="Cascadia Mono" FontSize="10" HorizontalAlignment="Center" VerticalAlignment="Center" />
        </Border>
        <Grid Grid.Column="2" Width="220" VerticalAlignment="Center">
            <StackPanel Orientation="Horizontal" Spacing="8" HorizontalAlignment="Center" Visibility="{Binding ActionEditorVisibility}">
                <NumberBox Value="{Binding ActionValue, Mode=TwoWay}" Minimum="1" Maximum="8000000" SmallChange="100" Width="136" MinWidth="0" SpinButtonPlacementMode="Compact" />
                <Button Content="{Binding ApplyLabel}" Command="{Binding ExecuteCommand}" MinWidth="68" />
            </StackPanel>
            <ToggleSwitch IsOn="{Binding IsEnabled, Mode=OneWay}" IsHitTestVisible="False" OffContent="" OnContent="" Width="52" HorizontalAlignment="Center" Visibility="{Binding ToggleVisibility}" />
        </Grid>
        <TextBlock Grid.Column="3" Text="{Binding Status}" FontSize="10" Foreground="{ThemeResource TextFillColorSecondaryBrush}" HorizontalAlignment="Right" VerticalAlignment="Center" TextTrimming="CharacterEllipsis" />
    </Grid>
</DataTemplate>
""";

    private const string CompactTemplateXaml = """
<DataTemplate xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation">
    <Grid MinHeight="70" Padding="10,6" RowSpacing="5">
        <Grid.RowDefinitions>
            <RowDefinition Height="Auto" />
            <RowDefinition Height="36" />
        </Grid.RowDefinitions>
        <Grid ColumnSpacing="10">
            <Grid.ColumnDefinitions>
                <ColumnDefinition Width="*" />
                <ColumnDefinition Width="Auto" />
            </Grid.ColumnDefinitions>
            <TextBlock Text="{Binding DisplayName}" FontSize="12" FontWeight="SemiBold" TextTrimming="CharacterEllipsis" VerticalAlignment="Center" />
            <TextBlock Grid.Column="1" Text="{Binding Status}" FontSize="10" Foreground="{ThemeResource TextFillColorSecondaryBrush}" TextTrimming="CharacterEllipsis" VerticalAlignment="Center" />
        </Grid>
        <Grid Grid.Row="1" ColumnSpacing="10">
            <Grid.ColumnDefinitions>
                <ColumnDefinition Width="104" />
                <ColumnDefinition Width="*" />
                <ColumnDefinition Width="220" />
            </Grid.ColumnDefinitions>
            <Border Width="104" Height="30" Background="{ThemeResource CardBackgroundFillColorSecondaryBrush}" CornerRadius="6" VerticalAlignment="Center">
                <TextBlock Text="{Binding Shortcut}" FontFamily="Cascadia Mono" FontSize="10" HorizontalAlignment="Center" VerticalAlignment="Center" />
            </Border>
            <TextBlock Grid.Column="1" Text="{Binding Scope}" FontSize="10" Foreground="{ThemeResource TextFillColorSecondaryBrush}" VerticalAlignment="Center" TextTrimming="CharacterEllipsis" />
            <Grid Grid.Column="2" Width="220" VerticalAlignment="Center">
                <StackPanel Orientation="Horizontal" Spacing="8" HorizontalAlignment="Right" Visibility="{Binding ActionEditorVisibility}">
                    <NumberBox Value="{Binding ActionValue, Mode=TwoWay}" Minimum="1" Maximum="8000000" SmallChange="100" Width="136" MinWidth="0" SpinButtonPlacementMode="Compact" />
                    <Button Content="{Binding ApplyLabel}" Command="{Binding ExecuteCommand}" MinWidth="68" />
                </StackPanel>
                <ToggleSwitch IsOn="{Binding IsEnabled, Mode=OneWay}" IsHitTestVisible="False" OffContent="" OnContent="" Width="52" HorizontalAlignment="Right" Visibility="{Binding ToggleVisibility}" />
            </Grid>
        </Grid>
    </Grid>
</DataTemplate>
""";

    public static DataTemplate CreateMainTemplate() => (DataTemplate)XamlReader.Load(MainTemplateXaml);
    public static DataTemplate CreateCompactTemplate() => (DataTemplate)XamlReader.Load(CompactTemplateXaml);
}