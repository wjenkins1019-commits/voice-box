using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;

namespace VoiceBox
{
    // Slim dark scrollbars, declared in XAML so the Track/Thumb wiring stays
    // exactly as WPF expects it. Returns null if the markup cannot be parsed,
    // in which case the stock scrollbars are used.
    public static class ScrollTheme
    {
        public static Style Dark()
        {
            string xaml = @"<Style xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation'
       xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml'
       TargetType='{x:Type ScrollBar}'>
  <Setter Property='Background' Value='#05121F'/>
  <Setter Property='Template'>
    <Setter.Value>
      <ControlTemplate TargetType='{x:Type ScrollBar}'>
        <Border Background='#071527' CornerRadius='6'>
          <Track Name='PART_Track' Orientation='Horizontal'
                 Minimum='{TemplateBinding Minimum}'
                 Maximum='{TemplateBinding Maximum}'
                 Value='{TemplateBinding Value}'
                 ViewportSize='{TemplateBinding ViewportSize}'>
            <Track.Thumb>
              <Thumb>
                <Thumb.Template>
                  <ControlTemplate TargetType='{x:Type Thumb}'>
                    <Border CornerRadius='6' Background='#78C3FF' BorderBrush='#C6ECFF' BorderThickness='1' Margin='1'/>
                  </ControlTemplate>
                </Thumb.Template>
              </Thumb>
            </Track.Thumb>
          </Track>
        </Border>
      </ControlTemplate>
    </Setter.Value>
  </Setter>
  <Style.Triggers>
    <Trigger Property='Orientation' Value='Horizontal'>
      <Setter Property='Height' Value='11'/>
    </Trigger>
    <Trigger Property='Orientation' Value='Vertical'>
      <Setter Property='Width' Value='11'/>
      <Setter Property='Template'>
        <Setter.Value>
          <ControlTemplate TargetType='{x:Type ScrollBar}'>
            <Border Background='#071527' CornerRadius='6'>
              <Track Name='PART_Track' Orientation='Vertical' IsDirectionReversed='True'
                     Minimum='{TemplateBinding Minimum}'
                     Maximum='{TemplateBinding Maximum}'
                     Value='{TemplateBinding Value}'
                     ViewportSize='{TemplateBinding ViewportSize}'>
                <Track.Thumb>
                  <Thumb>
                    <Thumb.Template>
                      <ControlTemplate TargetType='{x:Type Thumb}'>
                        <Border CornerRadius='6' Background='#78C3FF' BorderBrush='#C6ECFF' BorderThickness='1' Margin='1'/>
                      </ControlTemplate>
                    </Thumb.Template>
                  </Thumb>
                </Track.Thumb>
              </Track>
            </Border>
          </ControlTemplate>
        </Setter.Value>
      </Setter>
    </Trigger>
  </Style.Triggers>
</Style>";
            try
            {
                return (Style)XamlReader.Parse(xaml);
            }
            catch (Exception)
            {
                return null;
            }
        }
    }
}
