public static class Ui {
    public const string MainXaml = @"
<Grid xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation'
      xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml'
      Background='#F5F5F7'
      TextOptions.TextFormattingMode='Display'
      UseLayoutRounding='True'
      SnapsToDevicePixels='True'>
  <Grid.Resources>
    <Style x:Key='Card' TargetType='Border'>
      <Setter Property='Background' Value='White'/>
      <Setter Property='CornerRadius' Value='14'/>
      <Setter Property='BorderBrush' Value='#E8EDF6'/>
      <Setter Property='BorderThickness' Value='1'/>
      <Setter Property='Padding' Value='18,14'/>
      <Setter Property='Margin' Value='0,0,0,10'/>
      <Setter Property='Effect'>
        <Setter.Value>
          <DropShadowEffect BlurRadius='16' ShadowDepth='2' Opacity='0.05' Color='#1B2A4A' Direction='270'/>
        </Setter.Value>
      </Setter>
    </Style>

    <Style x:Key='Primary' TargetType='Button'>
      <Setter Property='Foreground' Value='White'/>
      <Setter Property='FontSize' Value='14'/>
      <Setter Property='FontWeight' Value='SemiBold'/>
      <Setter Property='Cursor' Value='Hand'/>
      <Setter Property='Height' Value='44'/>
      <Setter Property='Padding' Value='22,0'/>
      <Setter Property='Template'>
        <Setter.Value>
          <ControlTemplate TargetType='Button'>
            <Border x:Name='b' Background='#101215' CornerRadius='11'>
              <ContentPresenter HorizontalAlignment='Center' VerticalAlignment='Center' Margin='{TemplateBinding Padding}'/>
            </Border>
            <ControlTemplate.Triggers>
              <Trigger Property='IsMouseOver' Value='True'><Setter TargetName='b' Property='Background' Value='#22262C'/></Trigger>
              <Trigger Property='IsPressed' Value='True'><Setter TargetName='b' Property='Background' Value='#000000'/></Trigger>
              <Trigger Property='IsEnabled' Value='False'><Setter TargetName='b' Property='Background' Value='#C9C9CE'/></Trigger>
            </ControlTemplate.Triggers>
          </ControlTemplate>
        </Setter.Value>
      </Setter>
    </Style>

    <Style x:Key='Ghost' TargetType='Button'>
      <Setter Property='Foreground' Value='#2A2D33'/>
      <Setter Property='FontSize' Value='13'/>
      <Setter Property='Cursor' Value='Hand'/>
      <Setter Property='Height' Value='40'/>
      <Setter Property='Padding' Value='14,0'/>
      <Setter Property='Template'>
        <Setter.Value>
          <ControlTemplate TargetType='Button'>
            <Border x:Name='b' Background='White' BorderBrush='#DCDDE2' BorderThickness='1' CornerRadius='10'>
              <ContentPresenter HorizontalAlignment='Center' VerticalAlignment='Center' Margin='{TemplateBinding Padding}'/>
            </Border>
            <ControlTemplate.Triggers>
              <Trigger Property='IsMouseOver' Value='True'>
                <Setter TargetName='b' Property='Background' Value='#F5F6F8'/>
                <Setter TargetName='b' Property='BorderBrush' Value='#C3C5CC'/>
              </Trigger>
              <Trigger Property='IsPressed' Value='True'><Setter TargetName='b' Property='Background' Value='#ECEDF0'/></Trigger>
              <Trigger Property='IsEnabled' Value='False'><Setter TargetName='b' Property='Opacity' Value='0.45'/></Trigger>
            </ControlTemplate.Triggers>
          </ControlTemplate>
        </Setter.Value>
      </Setter>
    </Style>

    <Style x:Key='IconBtn' TargetType='Button' BasedOn='{StaticResource Ghost}'>
      <Setter Property='Width' Value='42'/>
      <Setter Property='Padding' Value='0'/>
      <Setter Property='FontFamily' Value='Segoe MDL2 Assets'/>
      <Setter Property='FontSize' Value='14'/>
      <Setter Property='Foreground' Value='#3A3E46'/>
    </Style>

    <Style x:Key='Flat' TargetType='Button'>
      <Setter Property='Background' Value='Transparent'/>
      <Setter Property='BorderThickness' Value='0'/>
      <Setter Property='Foreground' Value='#5E6068'/>
      <Setter Property='FontSize' Value='13'/>
      <Setter Property='Cursor' Value='Hand'/>
      <Setter Property='Height' Value='32'/>
      <Setter Property='Padding' Value='13,0'/>
      <Setter Property='Template'>
        <Setter.Value>
          <ControlTemplate TargetType='Button'>
            <Border x:Name='b' Background='{TemplateBinding Background}' CornerRadius='8'>
              <ContentPresenter HorizontalAlignment='Center' VerticalAlignment='Center' Margin='{TemplateBinding Padding}'/>
            </Border>
            <ControlTemplate.Triggers>
              <Trigger Property='IsMouseOver' Value='True'><Setter TargetName='b' Property='Background' Value='#EDF1F9'/></Trigger>
              <Trigger Property='IsPressed' Value='True'><Setter TargetName='b' Property='Background' Value='#E2E9F6'/></Trigger>
            </ControlTemplate.Triggers>
          </ControlTemplate>
        </Setter.Value>
      </Setter>
    </Style>

    <Style x:Key='TabOn' TargetType='Button' BasedOn='{StaticResource Flat}'>
      <Setter Property='Background' Value='#101215'/>
      <Setter Property='Foreground' Value='White'/>
      <Setter Property='FontWeight' Value='SemiBold'/>
      <Setter Property='Template'>
        <Setter.Value>
          <ControlTemplate TargetType='Button'>
            <Border x:Name='b' Background='#101215' CornerRadius='8'>
              <ContentPresenter HorizontalAlignment='Center' VerticalAlignment='Center'/>
            </Border>
          </ControlTemplate>
        </Setter.Value>
      </Setter>
    </Style>

    <Style x:Key='Chk' TargetType='CheckBox'>
      <Setter Property='Cursor' Value='Hand'/>
      <Setter Property='Foreground' Value='#2A2D33'/>
      <Setter Property='FontSize' Value='12.5'/>
      <Setter Property='Template'>
        <Setter.Value>
          <ControlTemplate TargetType='CheckBox'>
            <StackPanel Orientation='Horizontal' Background='Transparent'>
              <Border x:Name='box' Width='18' Height='18' CornerRadius='5' Background='White'
                      BorderBrush='#C9CAD0' BorderThickness='1.4' VerticalAlignment='Center'>
                <TextBlock x:Name='tick' Text='&#xE73E;' FontFamily='Segoe MDL2 Assets' FontSize='10'
                           Foreground='White' HorizontalAlignment='Center' VerticalAlignment='Center'
                           Visibility='Collapsed'/>
              </Border>
              <ContentPresenter Margin='9,0,0,0' VerticalAlignment='Center'/>
            </StackPanel>
            <ControlTemplate.Triggers>
              <Trigger Property='IsChecked' Value='True'>
                <Setter TargetName='box' Property='Background' Value='#101215'/>
                <Setter TargetName='box' Property='BorderBrush' Value='#101215'/>
                <Setter TargetName='tick' Property='Visibility' Value='Visible'/>
              </Trigger>
              <Trigger Property='IsMouseOver' Value='True'>
                <Setter TargetName='box' Property='BorderBrush' Value='#9A9CA3'/>
              </Trigger>
              <Trigger Property='IsChecked' Value='True'>
                <Setter TargetName='box' Property='BorderBrush' Value='#101215'/>
              </Trigger>
              <Trigger Property='IsEnabled' Value='False'>
                <Setter Property='Opacity' Value='0.45'/>
              </Trigger>
            </ControlTemplate.Triggers>
          </ControlTemplate>
        </Setter.Value>
      </Setter>
    </Style>
    <Style TargetType='TextBlock'>
      <Setter Property='FontFamily' Value='Microsoft YaHei UI'/>
    </Style>
    <Style TargetType='TextBox'>
      <Setter Property='FontFamily' Value='Microsoft YaHei UI'/>
    </Style>
    <Style TargetType='CheckBox'>
      <Setter Property='FontFamily' Value='Microsoft YaHei UI'/>
      <Setter Property='VerticalContentAlignment' Value='Center'/>
    </Style>
  </Grid.Resources>

  <Grid Margin='22,16,22,16'>
    <Grid.RowDefinitions>
      <RowDefinition Height='Auto'/>
      <RowDefinition Height='*'/>
      <RowDefinition Height='Auto'/>
    </Grid.RowDefinitions>

    <!-- ================= 顶部 ================= -->
    <Grid Grid.Row='0' Margin='0,0,0,14'>
      <StackPanel Orientation='Horizontal'>
        <Border Width='44' Height='44' CornerRadius='12'>
          <Border.Background>
            <LinearGradientBrush StartPoint='0,0' EndPoint='1,1'>
              <GradientStop Color='#2A303A' Offset='0'/>
              <GradientStop Color='#0B0E13' Offset='1'/>
            </LinearGradientBrush>
          </Border.Background>
          <TextBlock Text='&#xE88E;' FontFamily='Segoe MDL2 Assets' FontSize='21' Foreground='White'
                     HorizontalAlignment='Center' VerticalAlignment='Center'/>
        </Border>
        <StackPanel Margin='13,0,0,0' VerticalAlignment='Center'>
          <StackPanel Orientation='Horizontal'>
            <TextBlock Text='BootSmith 启动盘工具' FontSize='18.5' FontWeight='SemiBold' Foreground='#161B26'/>
            <Border Background='#ECEDF0' CornerRadius='6' Padding='7,2' Margin='10,0,0,0' VerticalAlignment='Center'>
              <TextBlock x:Name='LblVer' Text='1.0.1' FontSize='11' Foreground='#3A3E46'/>
            </Border>
          </StackPanel>
          <TextBlock Text='UOS / 麒麟 / Debian 启动盘 · 移除校验' FontSize='11.5' Foreground='#7A859B' Margin='0,3,0,0'/>
        </StackPanel>
      </StackPanel>

      <Border HorizontalAlignment='Right' VerticalAlignment='Center' Background='White' CornerRadius='10'
              BorderBrush='#E6EAF2' BorderThickness='1' Padding='4'>
        <StackPanel Orientation='Horizontal'>
          <Button x:Name='TabMake' Style='{StaticResource Flat}' Content='制作安装盘' Height='34' Padding='14,0'/>
          <Button x:Name='TabFix' Style='{StaticResource Flat}' Content='去校验 / 还原' Height='34' Padding='14,0' Margin='2,0,0,0'/>
        </StackPanel>
      </Border>
    </Grid>

    <!-- ================= 页面 ================= -->
    <Grid Grid.Row='1'>
      <!-- 制作页 -->
      <Grid x:Name='PageMake' Visibility='Visible' Focusable='False' FocusVisualStyle='{x:Null}'>
        <Grid.RowDefinitions>
          <RowDefinition Height='*'/>
          <RowDefinition Height='Auto'/>
        </Grid.RowDefinitions>

        <ScrollViewer Grid.Row='0' VerticalScrollBarVisibility='Auto' Padding='0,0,4,0' Focusable='False'>
          <StackPanel>
            <!-- 安装镜像 -->
            <Border Style='{StaticResource Card}'>
              <StackPanel>
                <StackPanel Orientation='Horizontal' Margin='0,0,0,10'>
                  <TextBlock Text='&#xE7F4;' FontFamily='Segoe MDL2 Assets' FontSize='14' Foreground='#3A3E46' VerticalAlignment='Center'/>
                  <TextBlock Text='安装镜像' FontSize='14' FontWeight='SemiBold' Foreground='#1B1F27' Margin='8,0,0,0' VerticalAlignment='Center'/>
                  <TextBlock Text='.iso · 必选' FontSize='11.5' Foreground='#8A94A8' Margin='8,2,0,0' VerticalAlignment='Center'/>
                </StackPanel>
                <Grid>
                  <Grid.ColumnDefinitions>
                    <ColumnDefinition Width='*'/>
                    <ColumnDefinition Width='Auto'/>
                  </Grid.ColumnDefinitions>
                  <Border Background='#F7F9FD' BorderBrush='#E3E9F4' BorderThickness='1' CornerRadius='10' Height='40' Padding='12,0'>
                    <TextBlock x:Name='TxtIso' VerticalAlignment='Center' FontSize='12.5' Foreground='#22304A'
                               TextTrimming='CharacterEllipsis' Text='（未选择）'/>
                  </Border>
                  <Button x:Name='BtnBrowseIso' Grid.Column='1' Style='{StaticResource IconBtn}' Content='&#xE712;' ToolTip='浏览…' Margin='10,0,0,0'/>
                </Grid>
                <TextBlock x:Name='LblIsoInfo' FontSize='11.5' Foreground='#8A94A8' Margin='2,7,0,0' TextTrimming='CharacterEllipsis'
                           Text='未选择安装镜像'/>
              </StackPanel>
            </Border>

            <!-- 备份镜像 -->
            <Border Style='{StaticResource Card}'>
              <Grid>
                <Grid.ColumnDefinitions>
                  <ColumnDefinition Width='*'/>
                  <ColumnDefinition Width='Auto'/>
                </Grid.ColumnDefinitions>
                <StackPanel>
                  <StackPanel Orientation='Horizontal' Margin='0,0,0,10'>
                    <TextBlock Text='&#xE896;' FontFamily='Segoe MDL2 Assets' FontSize='14' Foreground='#3A3E46' VerticalAlignment='Center'/>
                    <TextBlock Text='备份还原镜像' FontSize='14' FontWeight='SemiBold' Foreground='#1B1F27' Margin='8,0,0,0' VerticalAlignment='Center'/>
                    <TextBlock Text='.uimg · 可选' FontSize='11.5' Foreground='#8A94A8' Margin='8,2,0,0' VerticalAlignment='Center'/>
                  </StackPanel>
                  <Grid>
                    <Grid.ColumnDefinitions>
                      <ColumnDefinition Width='*'/>
                      <ColumnDefinition Width='Auto'/>
                      <ColumnDefinition Width='Auto'/>
                    </Grid.ColumnDefinitions>
                    <Border Background='#F7F9FD' BorderBrush='#E3E9F4' BorderThickness='1' CornerRadius='10' Height='40' Padding='12,0'>
                      <TextBlock x:Name='TxtUimg' VerticalAlignment='Center' FontSize='12.5' Foreground='#22304A'
                                 TextTrimming='CharacterEllipsis' Text='（未选择 → 单分区）'/>
                    </Border>
                    <Button x:Name='BtnBrowseUimg' Grid.Column='1' Style='{StaticResource IconBtn}' Content='&#xE712;' ToolTip='浏览…' Margin='10,0,0,0'/>
                    <Button x:Name='BtnClearUimg' Grid.Column='2' Style='{StaticResource IconBtn}' Content='&#xE74D;' ToolTip='清空（回到单分区）' Margin='8,0,0,0'/>
                  </Grid>
                  <TextBlock x:Name='LblUimgInfo' FontSize='11.5' Foreground='#8A94A8' Margin='2,7,0,0' TextTrimming='CharacterEllipsis'
                             Text='未选择 → 单分区（整盘只做引导）'/>
                </StackPanel>

                <Border x:Name='Badge' Grid.Column='1' Background='#ECEDF0' BorderBrush='#E0E1E6' BorderThickness='1'
                        CornerRadius='10' Padding='14,9' Margin='14,26,0,0' VerticalAlignment='Top' MinWidth='150'>
                  <StackPanel>
                    <TextBlock x:Name='TxtScheme' Text='单分区' FontSize='14' FontWeight='SemiBold' Foreground='#2A2D33'/>
                    <TextBlock x:Name='TxtSchemeSub' Text='整盘 FAT32 · 仅引导' FontSize='11' Foreground='#7A7D85' Margin='0,3,0,0'
                               TextTrimming='CharacterEllipsis'/>
                  </StackPanel>
                </Border>
              </Grid>
            </Border>

            <!-- 制作选项 -->
            <Border Style='{StaticResource Card}'>
              <StackPanel>
                <StackPanel Orientation='Horizontal' Margin='0,0,0,10'>
                  <TextBlock Text='&#xE713;' FontFamily='Segoe MDL2 Assets' FontSize='14' Foreground='#3A3E46' VerticalAlignment='Center'/>
                  <TextBlock Text='制作选项' FontSize='14' FontWeight='SemiBold' Foreground='#1B1F27' Margin='8,0,0,0' VerticalAlignment='Center'/>
                </StackPanel>
                <Grid>
                  <Grid.ColumnDefinitions>
                    <ColumnDefinition Width='Auto'/>
                    <ColumnDefinition Width='Auto'/>
                    <ColumnDefinition Width='Auto'/>
                    <ColumnDefinition Width='Auto'/>
                    <ColumnDefinition Width='Auto'/>
                    <ColumnDefinition Width='*'/>
                  </Grid.ColumnDefinitions>
                  <TextBlock Grid.Column='0' Text='引导区大小' FontSize='12.5' Foreground='#5A6478' VerticalAlignment='Center'/>
                  <Border Grid.Column='1' Background='#F7F9FD' BorderBrush='#E3E9F4' BorderThickness='1' CornerRadius='8'
                          Height='34' Width='92' Padding='10,0' Margin='10,0,6,0'>
                    <TextBox x:Name='TxtP1' Text='16384' BorderThickness='0' Background='Transparent'
                             VerticalContentAlignment='Center' FontSize='12.5' Foreground='#22304A'/>
                  </Border>
                  <TextBlock Grid.Column='2' Text='MB' FontSize='12.5' Foreground='#8A94A8' VerticalAlignment='Center'/>
                  <StackPanel Grid.Column='5' Orientation='Horizontal' Margin='22,0,0,0' VerticalAlignment='Center'>
                    <CheckBox x:Name='ChkRemove' Style='{StaticResource Chk}' Content='同时移除安装器「校验镜像」' IsChecked='True'
                              VerticalAlignment='Center' FontSize='12.5' Foreground='#33405A'/>
                    <TextBlock x:Name='LblPatchState' Text='' FontSize='11.5' Foreground='#8A94A8' Margin='10,0,0,0'
                               VerticalAlignment='Center' TextTrimming='CharacterEllipsis'/>
                  </StackPanel>
                </Grid>
              </StackPanel>
            </Border>

            <!-- 目标盘 -->
            <Border Style='{StaticResource Card}'>
              <StackPanel>
                <Grid Margin='0,0,0,10'>
                  <StackPanel Orientation='Horizontal'>
                    <TextBlock Text='&#xE88E;' FontFamily='Segoe MDL2 Assets' FontSize='14' Foreground='#3A3E46' VerticalAlignment='Center'/>
                    <TextBlock Text='目标 U 盘' FontSize='14' FontWeight='SemiBold' Foreground='#1B1F27' Margin='8,0,0,0' VerticalAlignment='Center'/>
                    <TextBlock Text='数据会全部清空' FontSize='11.5' Foreground='#8A94A8' Margin='8,2,0,0' VerticalAlignment='Center'/>
                  </StackPanel>
                  <Button x:Name='BtnRefresh' Style='{StaticResource Flat}' Content='刷新' HorizontalAlignment='Right'/>
                </Grid>
                <Grid>
                  <ListBox x:Name='LstDisks' BorderThickness='0' Background='Transparent' MaxHeight='118'
                           ScrollViewer.HorizontalScrollBarVisibility='Disabled'>
                    <ListBox.ItemContainerStyle>
                      <Style TargetType='ListBoxItem'>
                        <Setter Property='Padding' Value='0'/>
                        <Setter Property='Margin' Value='0,0,0,7'/>
                        <Setter Property='HorizontalContentAlignment' Value='Stretch'/>
                        <Setter Property='Template'>
                          <Setter.Value>
                            <ControlTemplate TargetType='ListBoxItem'>
                              <Border x:Name='bd' Background='White' BorderBrush='#E3E9F4' BorderThickness='1'
                                      CornerRadius='10' Padding='13,9'>
                                <ContentPresenter/>
                              </Border>
                              <ControlTemplate.Triggers>
                                <Trigger Property='IsMouseOver' Value='True'>
                                  <Setter TargetName='bd' Property='Background' Value='#F7FAFF'/>
                                  <Setter TargetName='bd' Property='BorderBrush' Value='#C7D8F5'/>
                                </Trigger>
                                <Trigger Property='IsSelected' Value='True'>
                                  <Setter TargetName='bd' Property='Background' Value='#EFF5FF'/>
                                  <Setter TargetName='bd' Property='BorderBrush' Value='#101215'/>
                                </Trigger>
                              </ControlTemplate.Triggers>
                            </ControlTemplate>
                          </Setter.Value>
                        </Setter>
                      </Style>
                    </ListBox.ItemContainerStyle>
                    <ListBox.ItemTemplate>
                      <DataTemplate>
                        <Grid>
                          <Grid.ColumnDefinitions>
                            <ColumnDefinition Width='Auto'/>
                            <ColumnDefinition Width='*'/>
                            <ColumnDefinition Width='Auto'/>
                          </Grid.ColumnDefinitions>
                          <TextBlock Text='&#xE88E;' FontFamily='Segoe MDL2 Assets' FontSize='18' Foreground='#3A3E46' VerticalAlignment='Center'/>
                          <StackPanel Grid.Column='1' Margin='11,0,10,0'>
                            <TextBlock Text='{Binding Num}' FontSize='13' Foreground='#1B1F27' TextTrimming='CharacterEllipsis'/>
                            <TextBlock FontSize='11' Foreground='#8A94A8' Margin='0,2,0,0' TextTrimming='CharacterEllipsis'>
                              <Run Text='{Binding Title}'/><Run Text='  ·  '/><Run Text='{Binding Info}'/>
                            </TextBlock>
                          </StackPanel>
                          <TextBlock Grid.Column='2' Text='{Binding State}' Foreground='{Binding StateBrush}'
                                     FontSize='11.5' VerticalAlignment='Center'/>
                        </Grid>
                      </DataTemplate>
                    </ListBox.ItemTemplate>
                  </ListBox>
                  <TextBlock x:Name='LblNoDisk' Visibility='Collapsed' Text='没检测到 USB 磁盘 · 换根线 / 换个 USB 口，再点右上「刷新」'
                             FontSize='12' Foreground='#9AA4B8' HorizontalAlignment='Center' VerticalAlignment='Center' Margin='0,18,0,18'/>
                </Grid>
              </StackPanel>
            </Border>
          </StackPanel>
        </ScrollViewer>

        <!-- 日志（浮在内容之上，不挤压卡片） -->
        <Border x:Name='LogCard' Grid.Row='0' Style='{StaticResource Card}' Padding='12,10' Margin='0,0,4,0'
                VerticalAlignment='Bottom' Visibility='Collapsed'
                Background='#FBFCFE' BorderBrush='#DDE5F2'>
          <TextBox x:Name='LogBox' BorderThickness='0' Background='Transparent' IsReadOnly='True'
                   FontFamily='Consolas' FontSize='11.5' Foreground='#3A465C' Height='118'
                   VerticalScrollBarVisibility='Auto' TextWrapping='NoWrap'/>
        </Border>
      </Grid>

      <!-- 去校验页 -->
      <Grid x:Name='PageFix' Visibility='Collapsed' Focusable='False' FocusVisualStyle='{x:Null}'>
        <Grid.RowDefinitions>
          <RowDefinition Height='Auto'/>
          <RowDefinition Height='*'/>
        </Grid.RowDefinitions>

        <Border Style='{StaticResource Card}'>
          <StackPanel>
            <Grid Margin='0,0,0,10'>
              <StackPanel Orientation='Horizontal'>
                <TextBlock Text='&#xE72E;' FontFamily='Segoe MDL2 Assets' FontSize='14' Foreground='#3A3E46' VerticalAlignment='Center'/>
                <TextBlock Text='选择 UOS 安装盘' FontSize='14' FontWeight='SemiBold' Foreground='#1B1F27' Margin='8,0,0,0' VerticalAlignment='Center'/>
                <TextBlock Text='只改引导与配置，不动系统镜像' FontSize='11.5' Foreground='#8A94A8' Margin='8,2,0,0' VerticalAlignment='Center'/>
              </StackPanel>
              <StackPanel Orientation='Horizontal' HorizontalAlignment='Right'>
                <Button x:Name='BtnCheckIso' Style='{StaticResource Ghost}' Content='检测 ISO 兼容性' Height='32' Padding='12,0' FontSize='12'/>
                <Button x:Name='BtnRefreshVols' Style='{StaticResource Flat}' Content='重新扫描' Margin='6,0,0,0'/>
              </StackPanel>
            </Grid>
            <ListBox x:Name='LstVols' BorderThickness='0' Background='Transparent' MaxHeight='116'
                     ScrollViewer.HorizontalScrollBarVisibility='Disabled'>
              <ListBox.ItemContainerStyle>
                <Style TargetType='ListBoxItem'>
                  <Setter Property='Padding' Value='0'/>
                  <Setter Property='Margin' Value='0,0,0,7'/>
                  <Setter Property='HorizontalContentAlignment' Value='Stretch'/>
                  <Setter Property='Template'>
                    <Setter.Value>
                      <ControlTemplate TargetType='ListBoxItem'>
                        <Border x:Name='bd' Background='White' BorderBrush='#E3E9F4' BorderThickness='1'
                                CornerRadius='10' Padding='13,9'>
                          <ContentPresenter/>
                        </Border>
                        <ControlTemplate.Triggers>
                          <Trigger Property='IsMouseOver' Value='True'>
                            <Setter TargetName='bd' Property='Background' Value='#F7FAFF'/>
                            <Setter TargetName='bd' Property='BorderBrush' Value='#C7D8F5'/>
                          </Trigger>
                          <Trigger Property='IsSelected' Value='True'>
                            <Setter TargetName='bd' Property='Background' Value='#EFF5FF'/>
                            <Setter TargetName='bd' Property='BorderBrush' Value='#101215'/>
                          </Trigger>
                        </ControlTemplate.Triggers>
                      </ControlTemplate>
                    </Setter.Value>
                  </Setter>
                </Style>
              </ListBox.ItemContainerStyle>
              <ListBox.ItemTemplate>
                <DataTemplate>
                  <Grid>
                    <Grid.ColumnDefinitions>
                      <ColumnDefinition Width='Auto'/>
                      <ColumnDefinition Width='*'/>
                    </Grid.ColumnDefinitions>
                    <TextBlock Text='&#xE88E;' FontFamily='Segoe MDL2 Assets' FontSize='18' Foreground='#3A3E46' VerticalAlignment='Center'/>
                    <StackPanel Grid.Column='1' Margin='11,0,0,0'>
                      <TextBlock Text='{Binding Title}' FontSize='13' Foreground='#1B1F27' TextTrimming='CharacterEllipsis'/>
                      <TextBlock Text='{Binding Info}' FontSize='11' Foreground='#8A94A8' Margin='0,2,0,0' TextTrimming='CharacterEllipsis'/>
                    </StackPanel>
                  </Grid>
                </DataTemplate>
              </ListBox.ItemTemplate>
            </ListBox>
            <Border Background='#F7F9FD' BorderBrush='#E7ECF5' BorderThickness='1' CornerRadius='9' Padding='12,9' Margin='0,10,0,0'>
              <TextBlock x:Name='LblFixState' FontSize='12' Foreground='#5A6478' TextWrapping='Wrap' Text='正在检测…'/>
            </Border>
            <StackPanel Orientation='Horizontal' Margin='0,12,0,0'>
              <Button x:Name='BtnFixRemove' Style='{StaticResource Primary}' Content='一键移除引导镜像校验'/>
              <Button x:Name='BtnFixRestore' Style='{StaticResource Ghost}' Content='恢复原厂校验' Margin='10,0,0,0'/>
            </StackPanel>
            <TextBlock FontSize='11.5' Foreground='#8A94A8' Margin='2,10,0,0' TextWrapping='Wrap'
                       Text='移除后安装器不再校验镜像签名，改过的镜像也能装；原文件自动备份为 *.orig，可随时还原。'/>
          </StackPanel>
        </Border>

        <Border Grid.Row='1' Style='{StaticResource Card}' Padding='12,10' Margin='0,0,4,0'>
          <TextBox x:Name='FixLog' BorderThickness='0' Background='Transparent' IsReadOnly='True'
                   FontFamily='Consolas' FontSize='11.5' Foreground='#3A465C'
                   VerticalScrollBarVisibility='Auto' VerticalAlignment='Stretch'/>
        </Border>
      </Grid>
    </Grid>
    <!-- ================= 底栏 ================= -->
    <Border Grid.Row='2' Background='White' CornerRadius='14' Padding='16,12' BorderBrush='#E8E8EC' BorderThickness='1'>
      <Border.Effect>
        <DropShadowEffect BlurRadius='16' ShadowDepth='2' Opacity='0.05' Color='#1B2A4A' Direction='270'/>
      </Border.Effect>
      <Grid>
        <Grid.ColumnDefinitions>
          <ColumnDefinition Width='Auto'/>
          <ColumnDefinition Width='*'/>
          <ColumnDefinition Width='Auto'/>
        </Grid.ColumnDefinitions>

        <Button x:Name='BtnStart' Style='{StaticResource Primary}' Content='开始制作' MinWidth='132' Height='46'/>

        <Grid Grid.Column='1' Margin='18,0,14,0' VerticalAlignment='Center'>
          <Grid.RowDefinitions>
            <RowDefinition Height='Auto'/>
            <RowDefinition Height='Auto'/>
            <RowDefinition Height='Auto'/>
          </Grid.RowDefinitions>
          <Border x:Name='Track' Height='8' CornerRadius='4' Background='#EDF1F8' ClipToBounds='True'>
            <Border x:Name='Fill' Width='0' HorizontalAlignment='Left' CornerRadius='4'>
              <Border.Background>
                <LinearGradientBrush StartPoint='0,0' EndPoint='1,0'>
                  <GradientStop Color='#4A4F57' Offset='0'/>
                  <GradientStop Color='#101215' Offset='1'/>
                </LinearGradientBrush>
              </Border.Background>
            </Border>
          </Border>
          <Grid Grid.Row='1' Margin='0,8,0,0'>
            <TextBlock x:Name='LblPhase' Text='就绪' FontSize='12' Foreground='#1B1F27' FontWeight='SemiBold'
                       TextTrimming='CharacterEllipsis' Margin='0,0,52,0'/>
            <TextBlock x:Name='LblPct' Text='—' HorizontalAlignment='Right' FontSize='13'
                       FontWeight='SemiBold' Foreground='#3A3E46'/>
          </Grid>
          <StackPanel Grid.Row='2' Orientation='Horizontal' Margin='0,3,0,0'>
            <TextBlock x:Name='LblSpeed' Text='速度 —' FontSize='11.5' Foreground='#5A6478'/>
            <TextBlock x:Name='LblEta' Text='剩余 —' FontSize='11.5' Foreground='#5A6478' Margin='14,0,0,0'/>
          </StackPanel>
        </Grid>

        <Button x:Name='BtnToggleLog' Grid.Column='2' Style='{StaticResource Ghost}' Content='日志' Height='34' Padding='14,0' VerticalAlignment='Center'/>
      </Grid>
    </Border>
  </Grid>
</Grid>
";
}
