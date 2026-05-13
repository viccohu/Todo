# WinUI3 原生主题资源完整参考指南

> **文档版本**: 1.0  
> **最后更新**: 2026-05-11  
> **参考来源**: [微软官方文档](https://learn.microsoft.com/windows/apps/develop/platform/xaml/xaml-theme-resources) | [WinUI3 Gallery](https://github.com/microsoft/WinUI-Gallery) | [Microsoft.UI.Xaml 源码](https://github.com/microsoft/microsoft-ui-xaml)

---

## 一、文档概述

本文档整理了 WinUI3 (Windows UI Library 3) 的完整原生主题资源，涵盖颜色系统、画笔资源、排版样式和控件模板。这些资源基于微软 Fluent Design System，可帮助开发者快速构建符合 Windows 11 设计规范的现代化应用。

### 1.1 核心概念

| 概念 | 说明 |
|------|------|
| **ThemeResource** | 主题资源标记扩展，在应用加载和主题切换时自动更新值 |
| **StaticResource** | 静态资源标记扩展，仅在应用首次加载时解析，之后不再更新 |
| **Light 主题** | 浅色模式，适合大多数用户 |
| **Dark 主题** | 深色模式，减少眼睛疲劳 |
| **HighContrast** | 高对比度模式，无障碍辅助功能 |

### 1.2 资源引用方式

```xml
<!-- ThemeResource：支持运行时主题切换 -->
<TextBlock Foreground="{ThemeResource TextFillColorPrimaryBrush}" />

<!-- StaticResource：仅在加载时解析一次 -->
<TextBlock Foreground="{StaticResource TextFillColorPrimaryBrush}" />
```

---

## 二、颜色系统

WinUI3 提供了一套完整的颜色资源，分为浅色主题（Light）、深色主题（Dark）和高对比度主题（HighContrast）三个版本。

### 2.1 文本颜色（Text Fill Colors）

文本颜色用于设置文字的显示颜色，按重要程度分为四个层级。

#### 颜色定义速查表

| 资源名称 | Light 主题 | Dark 主题 | 用途说明 |
|----------|-----------|-----------|----------|
| `TextFillColorPrimary` | `#E4000000` | `#FFFFFFFF` | 主要文本，高对比度 |
| `TextFillColorSecondary` | `#99000000` | `#C5FFFFFF` | 次要文本，中等对比度 |
| `TextFillColorTertiary` | `#61000000` | `#87FFFFFF` | 辅助文本，低对比度 |
| `TextFillColorDisabled` | `#5C000000` | `#5DFFFFFF` | 禁用状态文本 |

#### 使用示例

```xml
<!-- 主要文本 -->
<TextBlock Text="这是主要文本"
           Foreground="{ThemeResource TextFillColorPrimaryBrush}" />

<!-- 次要文本 -->
<TextBlock Text="这是次要文本"
           Foreground="{ThemeResource TextFillColorSecondaryBrush}" />

<!-- 辅助文本 -->
<TextBlock Text="这是辅助文本"
           Foreground="{ThemeResource TextFillColorTertiaryBrush}" />

<!-- 禁用状态 -->
<TextBlock Text="这是禁用文本"
           Foreground="{ThemeResource TextFillColorDisabledBrush}"
           IsEnabled="False" />
```

### 2.2 填充颜色（Fill Colors）

填充颜色用于设置控件和容器的背景色。

#### 控件填充色（Control Fill Colors）

| 资源名称 | Light 主题 | Dark 主题 | 用途说明 |
|----------|-----------|-----------|----------|
| `ControlFillColorDefault` | `#0A000000` | `#0FFFFFFF` | 默认填充色 |
| `ControlFillColorSecondary` | `#08000000` | `#15FFFFFF` | 次要填充色 |
| `ControlFillColorTertiary` | `#05000000` | `#08FFFFFF` | 第三填充色 |
| `ControlFillColorQuaternary` | `#0A000000` | `#0FFFFFFF` | 第四填充色 |
| `ControlFillColorDisabled` | `#0C000000` | `#0BFFFFFF` | 禁用填充色 |
| `ControlFillColorInputActive` | `#DEEBF3FF` | `#B31E1E1E` | 输入框激活状态 |

#### 强调色填充（Accent Fill Colors）

| 资源名称 | 说明 | 特殊说明 |
|----------|------|----------|
| `AccentFillColorDefaultBrush` | 默认强调色填充 | 跟随系统强调色 |
| `AccentFillColorSecondaryBrush` | 次要强调色填充 | 90% 不透明度 |
| `AccentFillColorTertiaryBrush` | 第三强调色填充 | 80% 不透明度 |
| `AccentFillColorDisabledBrush` | 禁用强调色填充 | 降低对比度 |

#### 背景填充色（Background Fill Colors）

| 资源名称 | Light 主题 | Dark 主题 | 用途说明 |
|----------|-----------|-----------|----------|
| `SolidBackgroundFillColorBase` | `#FFFFFF` | `#202020` | 基础背景 |
| `SolidBackgroundFillColorSecondary` | `#F3F3F3` | `#1C1C1C` | 次要背景 |
| `SolidBackgroundFillColorTertiary` | `#F9F9F9` | `#282828` | 第三背景 |
| `SolidBackgroundFillColorQuaternary` | `#FFFFFF` | `#2C2C2C` | 第四背景 |
| `SolidBackgroundFillColorQuinary` | `#FFFFFF` | `#333333` | 第五背景 |
| `SolidBackgroundFillColorSenary` | `#FFFFFF` | `#373737` | 第六背景 |
| `SolidBackgroundFillColorTransparent` | `#00FFFFFF` | `#00202020` | 透明背景 |

#### 使用示例

```xml
<!-- 卡片背景 -->
<Border Background="{ThemeResource CardBackgroundFillColorDefaultBrush}"
        BorderBrush="{ThemeResource CardStrokeColorDefaultBrush}"
        BorderThickness="1"
        CornerRadius="4"
        Padding="12">
    <TextBlock Text="卡片内容" />
</Border>

<!-- 次要背景 -->
<Grid Background="{ThemeResource SolidBackgroundFillColorSecondaryBrush}">
    <TextBlock Text="次要区域" />
</Grid>

<!-- 输入框 -->
<TextBox Background="{ThemeResource ControlFillColorInputActiveBrush}"
         BorderBrush="{ThemeResource ControlStrokeColorDefaultBrush}" />
```

### 2.3 边框颜色（Stroke Colors）

边框颜色用于设置控件和元素的边框、轮廓线。

| 资源名称 | Light 主题 | Dark 主题 | 用途说明 |
|----------|-----------|-----------|----------|
| `ControlStrokeColorDefault` | `#0A000000` | `#12FFFFFF` | 默认边框 |
| `ControlStrokeColorSecondary` | `#12000000` | `#18FFFFFF` | 次要边框 |
| `ControlStrongStrokeColorDefault` | `#4D000000` | `#8BFFFFFF` | 强边框（用于分隔） |
| `ControlStrongStrokeColorDisabled` | `#27000000` | `#28FFFFFF` | 禁用边框 |
| `CardStrokeColorDefault` | `#08000000` | `#19000000` | 卡片边框 |
| `DividerStrokeColorDefault` | `#08000000` | `#15FFFFFF` | 分割线 |

#### 使用示例

```xml
<!-- 分隔线 -->
<Border Height="1"
        Background="{ThemeResource DividerStrokeColorDefaultBrush}" />

<!-- 卡片边框 -->
<Border Background="{ThemeResource CardBackgroundFillColorDefaultBrush}"
        BorderBrush="{ThemeResource CardStrokeColorDefaultBrush}"
        BorderThickness="1"
        CornerRadius="8" />

<!-- 强边框分隔 -->
<Border BorderBrush="{ThemeResource ControlStrongStrokeColorDefaultBrush}"
        BorderThickness="1" />
```

### 2.4 系统状态颜色（System Fill Colors）

系统状态颜色用于表示操作结果和状态指示。

| 资源名称 | Light 主题 | Dark 主题 | 用途说明 |
|----------|-----------|-----------|----------|
| `SystemFillColorSuccess` | `#107C10` | `#6CCB5F` | 成功状态 |
| `SystemFillColorCaution` | `#C19C00` | `#FCE100` | 警告状态 |
| `SystemFillColorCritical` | `#D13438` | `#FF99A4` | 危险/错误状态 |
| `SystemFillColorNeutral` | `#5C5C5C` | `#8BFFFFFF` | 中性状态 |
| `SystemFillColorAttention` | `#0078D4` | `#4CC2FF` | 关注/提示状态 |

#### 状态背景色

| 资源名称 | Light 主题 | Dark 主题 | 用途说明 |
|----------|-----------|-----------|----------|
| `SystemFillColorSuccessBackground` | `#DFF6DD` | `#393D1B` | 成功背景 |
| `SystemFillColorCautionBackground` | `#FFF4CE` | `#433519` | 警告背景 |
| `SystemFillColorCriticalBackground` | `#FDE7E9` | `#442726` | 危险背景 |
| `SystemFillColorNeutralBackground` | `#F3F3F3` | `#08FFFFFF` | 中性背景 |

#### 使用示例

```xml
<!-- 成功提示 -->
<Border Background="{ThemeResource SystemFillColorSuccessBackgroundBrush}"
        CornerRadius="4"
        Padding="8,4">
    <StackPanel Orientation="Horizontal">
        <SymbolIcon Symbol="Accept" Foreground="{ThemeResource SystemFillColorSuccessBrush}" />
        <TextBlock Text="操作成功" Foreground="{ThemeResource SystemFillColorSuccessBrush}" />
    </StackPanel>
</Border>

<!-- 错误提示 -->
<Border Background="{ThemeResource SystemFillColorCriticalBackgroundBrush}"
        CornerRadius="4"
        Padding="8,4">
    <StackPanel Orientation="Horizontal">
        <SymbolIcon Symbol="ErrorBadge" Foreground="{ThemeResource SystemFillColorCriticalBrush}" />
        <TextBlock Text="发生错误" Foreground="{ThemeResource SystemFillColorCriticalBrush}" />
    </StackPanel>
</Border>
```

---

## 三、画笔资源

WinUI3 提供了多种类型的画笔资源，用于实现不同的视觉效果。

### 3.1 实心颜色画笔（SolidColorBrush）

实心颜色画笔是最基础的画笔类型，用单一颜色填充区域。

#### 语法结构

```xml
<SolidColorBrush x:Key="[BrushName]Brush" Color="{ThemeResource [ColorName]}" />
```

#### 常用实心画笔速查

```xml
<!-- 文本画笔 -->
<SolidColorBrush x:Key="TextFillColorPrimaryBrush" />
<SolidColorBrush x:Key="TextFillColorSecondaryBrush" />
<SolidColorBrush x:Key="TextFillColorTertiaryBrush" />

<!-- 控件画笔 -->
<SolidColorBrush x:Key="ControlFillColorDefaultBrush" />
<SolidColorBrush x:Key="ControlFillColorSecondaryBrush" />
<SolidColorBrush x:Key="ControlAltFillColorQuarternaryBrush" />

<!-- 边框画笔 -->
<SolidColorBrush x:Key="ControlStrokeColorDefaultBrush" />
<SolidColorBrush x:Key="ControlStrongStrokeColorDefaultBrush" />

<!-- 背景画笔 -->
<SolidColorBrush x:Key="SolidBackgroundFillColorBaseBrush" />
<SolidColorBrush x:Key="CardBackgroundFillColorDefaultBrush" />
```

#### 直接使用十六进制颜色

```xml
<!-- 使用预定义颜色名 -->
<Rectangle Width="100" Height="100" Fill="Red" />

<!-- 使用十六进制 ARGB 格式 -->
<Rectangle Width="100" Height="100" Fill="#FFFF0000" />

<!-- 属性元素语法（可设置不透明度） -->
<Rectangle Width="100" Height="100">
    <Rectangle.Fill>
        <SolidColorBrush Color="Blue" Opacity="0.5" />
    </Rectangle.Fill>
</Rectangle>
```

### 3.2 亚克力画笔（AcrylicBrush）

亚克力画笔创建半透明、模糊的背景效果，模拟真实世界中的亚克力材质。

#### 属性说明

| 属性 | 类型 | 说明 |
|------|------|------|
| `BackgroundSource` | `AcrylicBackgroundSource` | 背景来源：`Backdrop`（模糊）或 `HostBackdrop`（主机模糊） |
| `TintColor` | `Color` | 色调颜色 |
| `TintOpacity` | `double` | 色调不透明度（0-1） |
| `TintTransitionDuration` | `TimeSpan` | 色调过渡动画时长 |

#### 使用示例

```xml
<!-- 基础亚克力背景 -->
<Grid>
    <Grid.Background>
        <AcrylicBrush BackgroundSource="Backdrop" TintOpacity="0.6" />
    </Grid.Background>
    <TextBlock Text="亚克力背景效果" />
</Grid>

<!-- 带色调的亚克力 -->
<Border>
    <Border.Background>
        <AcrylicBrush BackgroundSource="Backdrop"
                       TintColor="#FF0078D4"
                       TintOpacity="0.7" />
    </Border.Background>
</Border>

<!-- 代码中创建亚克力画笔 -->
<Page.Resources>
    <AcrylicBrush x:Key="MyAcrylicBrush"
                   BackgroundSource="Backdrop"
                   TintColor="{ThemeResource SystemAccentColor}"
                   TintOpacity="0.6" />
</Page.Resources>
```

### 3.3 云母背景（MicaBackdrop）

Mica（云母）是 Windows 11 引入的新一代背景材质，提供比亚克力更丰富的视觉效果。

#### 使用方式

```xml
<!-- 在窗口级别应用 Mica -->
<Window ...>
    <Window.SystemBackdrop>
        <MicaBackdrop />
    </Window.SystemBackdrop>
    <Grid>
        <!-- 页面内容 -->
    </Grid>
</Window>

<!-- 设置主题色 -->
<Window.SystemBackdrop>
    <MicaBackdrop TintOpacity="0.5"
                  LuminosityOpacity="0.8" />
</Window.SystemBackdrop>

<!-- 在页面上应用 -->
<Page>
    <Page.Background>
        <MicaBackdrop />
    </Page.Background>
</Page>
```

#### Mica 与 Acrylic 对比

| 特性 | Mica | Acrylic |
|------|------|---------|
| Windows 版本 | Windows 11 专用 | Windows 10+ |
| 视觉效果 | 更丰富、更深沉 | 半透明模糊 |
| 性能消耗 | 较低 | 中等 |
| 推荐场景 | 主窗口背景 | 弹出菜单、浮层 |

### 3.4 渐变画笔

#### 线性渐变画笔（LinearGradientBrush）

```xml
<!-- 对角线渐变（默认值） -->
<Rectangle Width="200" Height="100">
    <Rectangle.Fill>
        <LinearGradientBrush>
            <GradientStop Color="Yellow" Offset="0.0" />
            <GradientStop Color="Red" Offset="0.25" />
            <GradientStop Color="Blue" Offset="0.75" />
            <GradientStop Color="LimeGreen" Offset="1.0" />
        </LinearGradientBrush>
    </Rectangle.Fill>
</Rectangle>

<!-- 水平渐变 -->
<Rectangle Width="200" Height="100">
    <Rectangle.Fill>
        <LinearGradientBrush StartPoint="0,0" EndPoint="1,0">
            <GradientStop Color="#0078D4" Offset="0.0" />
            <GradientStop Color="#00BCF2" Offset="1.0" />
        </LinearGradientBrush>
    </Rectangle.Fill>
</Rectangle>

<!-- 垂直渐变 -->
<Rectangle Width="200" Height="100">
    <Rectangle.Fill>
        <LinearGradientBrush StartPoint="0,0" EndPoint="0,1">
            <GradientStop Color="#1A1A1A" Offset="0.0" />
            <GradientStop Color="#2D2D2D" Offset="1.0" />
        </LinearGradientBrush>
    </Rectangle.Fill>
</Rectangle>
```

#### 径向渐变画笔（RadialGradientBrush）

```xml
<!-- 径向渐变 -->
<Rectangle Width="200" Height="200">
    <Rectangle.Fill>
        <media:RadialGradientBrush>
            <GradientStop Color="Blue" Offset="0.0" />
            <GradientStop Color="Yellow" Offset="0.2" />
            <GradientStop Color="LimeGreen" Offset="0.4" />
            <GradientStop Color="LightBlue" Offset="0.6" />
            <GradientStop Color="Blue" Offset="0.8" />
            <GradientStop Color="LightGray" Offset="1.0" />
        </media:RadialGradientBrush>
    </Rectangle.Fill>
</Rectangle>

<!-- 带偏移的径向渐变 -->
<Ellipse Width="200" Height="200">
    <Ellipse.Fill>
        <media:RadialGradientBrush GradientOrigin="0.3,0.3">
            <GradientStop Color="#0078D4" Offset="0.0" />
            <GradientStop Color="#004C8C" Offset="1.0" />
        </media:RadialGradientBrush>
    </Ellipse.Fill>
</Ellipse>
```

---

## 四、排版系统

WinUI3 定义了一套完整的字体排版规范，称为 Windows Type Ramp。

### 4.1 字体样式速查表

| 样式名称 | 资源键 | 字体重量 | 字号 | 用途 |
|----------|--------|----------|------|------|
| Caption | `CaptionTextBlockStyle` | Regular | 12px | 注释、说明文字 |
| Body | `BodyTextBlockStyle` | Regular | 14px | 正文内容 |
| Body Strong | `BodyStrongTextBlockStyle` | SemiBold | 14px | 重要正文 |
| Body Large | `BodyLargeTextBlockStyle` | Regular | 18px | 大段正文 |
| Body Large Strong | `BodyLargeStrongTextBlockStyle` | SemiBold | 18px | 重要大段正文 |
| Subtitle | `SubtitleTextBlockStyle` | SemiBold | 20px | 副标题 |
| Title | `TitleTextBlockStyle` | SemiBold | 28px | 标题 |
| Title Large | `TitleLargeTextBlockStyle` | SemiBold | 40px | 大标题 |
| Display | `DisplayTextBlockStyle` | SemiBold | 68px | 展示文字 |

### 4.2 使用示例

```xml
<!-- 显示所有文本样式 -->
<StackPanel Spacing="16">

    <!-- 展示文字 - 用于欢迎页、大字展示 -->
    <TextBlock Text="Display: 68px SemiBold"
               Style="{StaticResource DisplayTextBlockStyle}" />

    <!-- 大标题 - 用于页面主标题 -->
    <TextBlock Text="Title Large: 40px SemiBold"
               Style="{StaticResource TitleLargeTextBlockStyle}" />

    <!-- 标题 - 用于区块标题 -->
    <TextBlock Text="Title: 28px SemiBold"
               Style="{StaticResource TitleTextBlockStyle}" />

    <!-- 副标题 - 用于卡片标题 -->
    <TextBlock Text="Subtitle: 20px SemiBold"
               Style="{StaticResource SubtitleTextBlockStyle}" />

    <!-- 大正文 -->
    <TextBlock Text="Body Large: 18px Regular"
               Style="{StaticResource BodyLargeTextBlockStyle}" />

    <!-- 重要正文 -->
    <TextBlock Text="Body Strong: 14px SemiBold"
               Style="{StaticResource BodyStrongTextBlockStyle}" />

    <!-- 正文 -->
    <TextBlock Text="Body: 14px Regular"
               Style="{StaticResource BodyTextBlockStyle}" />

    <!-- 注释 -->
    <TextBlock Text="Caption: 12px Regular"
               Style="{StaticResource CaptionTextBlockStyle}" />
</StackPanel>
```

### 4.3 RichTextBlock 样式

```xml
<!-- 基础 RichTextBlock 样式 -->
<RichTextBlock Style="{StaticResource BaseRichTextBlockStyle}">
    <Paragraph>这是富文本内容。</Paragraph>
</RichTextBlock>

<!-- 正文 RichTextBlock 样式 -->
<RichTextBlock Style="{StaticResource BodyRichTextBlockStyle}">
    <Paragraph>
        <Run Text="这是" />
        <Run Text="加粗" FontWeight="SemiBold" />
        <Run Text="富文本内容。" />
    </Paragraph>
</RichTextBlock>
```

### 4.4 自定义文本样式

```xml
<Page.Resources>
    <!-- 自定义标题样式 -->
    <Style x:Key="CustomHeaderStyle" TargetType="TextBlock">
        <Setter Property="FontFamily" Value="Segoe UI Variable" />
        <Setter Property="FontWeight" Value="SemiBold" />
        <Setter Property="FontSize" Value="24" />
        <Setter Property="Foreground" Value="{ThemeResource TextFillColorPrimaryBrush}" />
    </Style>

    <!-- 自定义正文样式 -->
    <Style x:Key="CustomBodyStyle" TargetType="TextBlock">
        <Setter Property="FontFamily" Value="Segoe UI Variable" />
        <Setter Property="FontWeight" Value="Normal" />
        <Setter Property="FontSize" Value="14" />
        <Setter Property="Foreground" Value="{ThemeResource TextFillColorSecondaryBrush}" />
        <Setter Property="TextWrapping" Value="Wrap" />
    </Style>
</Page.Resources>
```

---

## 五、控件样式模板

### 5.1 按钮样式

#### 导航返回按钮

```xml
<!-- 普通尺寸导航按钮 (40x40) -->
<Button Style="{StaticResource NavigationBackButtonNormalStyle}" />

<!-- 小尺寸导航按钮 (30x30) -->
<Button Style="{StaticResource NavigationBackButtonSmallStyle}" />
```

#### 自定义按钮样式

```xml
<Page.Resources>
    <!-- 主要按钮 -->
    <Style x:Key="PrimaryButtonStyle" TargetType="Button">
        <Setter Property="Background" Value="{ThemeResource AccentFillColorDefaultBrush}" />
        <Setter Property="Foreground" Value="{ThemeResource TextOnAccentFillColorPrimaryBrush}" />
        <Setter Property="FontWeight" Value="SemiBold" />
        <Setter Property="Padding" Value="16,8" />
        <Setter Property="CornerRadius" Value="4" />
    </Style>

    <!-- 次要按钮 -->
    <Style x:Key="SecondaryButtonStyle" TargetType="Button">
        <Setter Property="Background" Value="{ThemeResource ControlFillColorDefaultBrush}" />
        <Setter Property="Foreground" Value="{ThemeResource TextFillColorPrimaryBrush}" />
        <Setter Property="BorderBrush" Value="{ThemeResource ControlStrokeColorDefaultBrush}" />
        <Setter Property="BorderThickness" Value="1" />
        <Setter Property="Padding" Value="16,8" />
        <Setter Property="CornerRadius" Value="4" />
    </Style>

    <!-- 幽灵按钮 -->
    <Style x:Key="GhostButtonStyle" TargetType="Button">
        <Setter Property="Background" Value="Transparent" />
        <Setter Property="Foreground" Value="{ThemeResource TextFillColorPrimaryBrush}" />
        <Setter Property="Padding" Value="8,4" />
    </Style>
</Page.Resources>

<!-- 使用示例 -->
<StackPanel Orientation="Horizontal" Spacing="8">
    <Button Content="主要操作" Style="{StaticResource PrimaryButtonStyle}" />
    <Button Content="次要操作" Style="{StaticResource SecondaryButtonStyle}" />
    <Button Content="幽灵按钮" Style="{StaticResource GhostButtonStyle}" />
</StackPanel>
```

### 5.2 卡片组件

```xml
<!-- 基础卡片 -->
<Border Background="{ThemeResource CardBackgroundFillColorDefaultBrush}"
        BorderBrush="{ThemeResource CardStrokeColorDefaultBrush}"
        BorderThickness="1"
        CornerRadius="8"
        Padding="16">
    <StackPanel>
        <TextBlock Text="卡片标题"
                   Style="{StaticResource SubtitleTextBlockStyle}" />
        <TextBlock Text="卡片内容描述文字"
                   Foreground="{ThemeResource TextFillColorSecondaryBrush}"
                   TextWrapping="Wrap" />
    </StackPanel>
</Border>

<!-- 可点击卡片 -->
<Button Padding="0">
    <Button.Template>
        <ControlTemplate TargetType="Button">
            <Border x:Name="CardBorder"
                    Background="{ThemeResource CardBackgroundFillColorDefaultBrush}"
                    BorderBrush="{ThemeResource CardStrokeColorDefaultBrush}"
                    BorderThickness="1"
                    CornerRadius="8"
                    Padding="16"
                    PointerEntered="CardBorder_PointerEntered"
                    PointerExited="CardBorder_PointerExited">
                <StackPanel>
                    <TextBlock Text="可点击卡片"
                               Style="{StaticResource SubtitleTextBlockStyle}" />
                    <TextBlock Text="点击此卡片执行操作"
                               Foreground="{ThemeResource TextFillColorSecondaryBrush}" />
                </StackPanel>
            </Border>
            <ControlTemplate.Triggers>
                <Trigger Property="IsPointerOver" Value="True">
                    <Setter TargetName="CardBorder" Property="Background"
                            Value="{ThemeResource ControlFillColorSecondaryBrush}" />
                </Trigger>
            </ControlTemplate.Triggers>
        </ControlTemplate>
    </Button.Template>
</Button>
```

### 5.3 输入控件

```xml
<!-- 文本输入框 -->
<TextBox PlaceholderText="请输入内容"
         Background="{ThemeResource ControlFillColorInputActiveBrush}"
         BorderBrush="{ThemeResource ControlStrokeColorDefaultBrush}"
         Padding="12,8" />

<!-- 带图标的输入框 -->
<Grid>
    <Grid.ColumnDefinitions>
        <ColumnDefinition Width="Auto" />
        <ColumnDefinition Width="*" />
    </Grid.ColumnDefinitions>
    <SymbolIcon Symbol="Search" Grid.Column="0" Margin="12,0" />
    <TextBox Grid.Column="1"
             PlaceholderText="搜索..."
             BorderThickness="0"
             Background="Transparent" />
</Grid>

<!-- 密码输入框 -->
<PasswordBox PlaceholderText="请输入密码"
             PasswordChar="●"
             Background="{ThemeResource ControlFillColorInputActiveBrush}" />
```

### 5.4 列表和列表项

```xml
<!-- 列表视图项样式 -->
<ListView>
    <ListView.ItemTemplate>
        <DataTemplate>
            <Grid Padding="12,8">
                <Grid.ColumnDefinitions>
                    <ColumnDefinition Width="Auto" />
                    <ColumnDefinition Width="*" />
                    <ColumnDefinition Width="Auto" />
                </Grid.ColumnDefinitions>

                <!-- 状态图标 -->
                <CheckBox Grid.Column="0"
                          IsChecked="{Binding IsCompleted}"
                          Margin="0,0,12,0" />

                <!-- 内容 -->
                <StackPanel Grid.Column="1">
                    <TextBlock Text="{Binding Title}"
                               Style="{StaticResource BodyTextBlockStyle}" />
                    <TextBlock Text="{Binding Description}"
                               Style="{StaticResource CaptionTextBlockStyle}"
                               Foreground="{ThemeResource TextFillColorTertiaryBrush}" />
                </StackPanel>

                <!-- 日期 -->
                <TextBlock Grid.Column="2"
                           Text="{Binding DueDate}"
                           Style="{StaticResource CaptionTextBlockStyle}"
                           Foreground="{ThemeResource TextFillColorTertiaryBrush}" />
            </Grid>
        </DataTemplate>
    </ListView.ItemTemplate>
</ListView>
```

---

## 六、主题切换

### 6.1 静态主题设置

在 `App.xaml` 中设置应用默认主题：

```xml
<Application>
    <Application.RequestedTheme>Dark</Application.RequestedTheme>
</Application>
```

可用值：`Light`、`Dark`、`System`（跟随系统设置）

### 6.2 运行时主题切换

```csharp
// 获取当前窗口
var window = App.MainWindow as Microsoft.UI.Xaml.Window;

// 获取应用级资源
var resources = window.Content is FrameworkElement root
    ? root.Resources
    : Application.Current.Resources;

// 设置浅色主题
window.AppWindow.TitleBar.PreferredTheme = Microsoft.UI.TitleBar.TitleBarTheme.Light;

// 或使用扩展方法（需要 Microsoft.UI
private void SetTheme(ElementTheme theme)
{
    if (Window.Current.Content is FrameworkElement root)
    {
        root.RequestedTheme = theme;
    }
}

// 使用示例
SetTheme(ElementTheme.Dark);   // 深色主题
SetTheme(ElementTheme.Light); // 浅色主题
SetTheme(ElementTheme.System);// 跟随系统
```

### 6.3 自定义主题资源

#### 定义自定义主题字典

```xml
<ResourceDictionary>
    <ResourceDictionary.ThemeDictionaries>
        <!-- 浅色主题 -->
        <ResourceDictionary x:Key="Light">
            <SolidColorBrush x:Key="MyBrandColor" Color="#0078D4" />
            <SolidColorBrush x:Key="MyBrandBackground" Color="#F3F3F3" />
        </ResourceDictionary>

        <!-- 深色主题 -->
        <ResourceDictionary x:Key="Dark">
            <SolidColorBrush x:Key="MyBrandColor" Color="#4CC2FF" />
            <SolidColorBrush x:Key="MyBrandBackground" Color="#1C1C1C" />
        </ResourceDictionary>

        <!-- 高对比度主题 -->
        <ResourceDictionary x:Key="HighContrast">
            <SolidColorBrush x:Key="MyBrandColor" Color="{ThemeResource SystemColorWindowTextColor}" />
            <SolidColorBrush x:Key="MyBrandBackground" Color="{ThemeResource SystemColorWindowColor}" />
        </ResourceDictionary>
    </ResourceDictionary.ThemeDictionaries>
</ResourceDictionary>
```

#### 使用自定义主题资源

```xml
<!-- 使用 ThemeResource 支持运行时切换 -->
<Border Background="{ThemeResource MyBrandBackground}">
    <Button Background="{ThemeResource MyBrandColor}"
            Content="品牌按钮" />
</Border>
```

### 6.4 主题切换最佳实践

#### ✅ 正确做法

```xml
<!-- 在样式和模板中使用 ThemeResource -->
<Style TargetType="Button">
    <Setter Property="Background" Value="{ThemeResource ControlFillColorDefaultBrush}" />
    <Setter Property="Foreground" Value="{ThemeResource TextFillColorPrimaryBrush}" />
</Style>

<!-- 单独定义每个主题 -->
<ResourceDictionary.ThemeDictionaries>
    <ResourceDictionary x:Key="Light">
        <SolidColorBrush x:Key="CustomBrush" Color="#0078D4" />
    </ResourceDictionary>
    <ResourceDictionary x:Key="Dark">
        <SolidColorBrush x:Key="CustomBrush" Color="#4CC2FF" />
    </ResourceDictionary>
</ResourceDictionary.ThemeDictionaries>
```

#### ❌ 错误做法

```xml
<!-- 错误：使用 Default 而非 Light/Dark -->
<ResourceDictionary x:Key="Default">
    <SolidColorBrush x:Key="CustomBrush" Color="#0078D4" />
</ResourceDictionary>

<!-- 错误：在 ThemeDictionaries 内部使用 ThemeResource -->
<ResourceDictionary.ThemeDictionaries>
    <ResourceDictionary x:Key="Light">
        <SolidColorBrush x:Key="CustomBrush"
                         Color="{ThemeResource ControlFillColorDefault}" />
    </ResourceDictionary>
</ResourceDictionary.ThemeDictionaries>
```

---

## 七、实用代码片段

### 7.1 基础页面模板

```xml
<Page x:Class="MyApp.Views.MainPage"
      xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
      xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">

    <!-- 应用 Mica 背景 -->
    <Page.Background>
        <MicaBackdrop />
    </Page.Background>

    <Grid Padding="24">
        <Grid.RowDefinitions>
            <RowDefinition Height="Auto" />
            <RowDefinition Height="*" />
        </Grid.RowDefinitions>

        <!-- 页面标题 -->
        <TextBlock Grid.Row="0"
                   Text="页面标题"
                   Style="{StaticResource TitleTextBlockStyle}"
                   Margin="0,0,0,24" />

        <!-- 内容区域 -->
        <ScrollViewer Grid.Row="1">
            <!-- 在此添加内容 -->
        </ScrollViewer>
    </Grid>
</Page>
```

### 7.2 列表卡片模板

```xml
<ListView ItemsSource="{x:Bind ViewModel.Items}">
    <ListView.ItemContainerStyle>
        <Style TargetType="ListViewItem">
            <Setter Property="Padding" Value="0" />
            <Setter Property="Margin" Value="0,0,0,8" />
            <Setter Property="HorizontalContentAlignment" Value="Stretch" />
        </Style>
    </ListView.ItemContainerStyle>
    <ListView.ItemTemplate>
        <DataTemplate>
            <Border Background="{ThemeResource CardBackgroundFillColorDefaultBrush}"
                    BorderBrush="{ThemeResource CardStrokeColorDefaultBrush}"
                    BorderThickness="1"
                    CornerRadius="8"
                    Padding="16">
                <Grid>
                    <Grid.ColumnDefinitions>
                        <ColumnDefinition Width="Auto" />
                        <ColumnDefinition Width="*" />
                        <ColumnDefinition Width="Auto" />
                    </Grid.ColumnDefinitions>

                    <CheckBox Grid.Column="0"
                              IsChecked="{Binding IsDone}"
                              VerticalAlignment="Center" />

                    <StackPanel Grid.Column="1" Margin="12,0">
                        <TextBlock Text="{Binding Title}"
                                   Style="{StaticResource BodyStrongTextBlockStyle}" />
                        <TextBlock Text="{Binding Description}"
                                   Style="{StaticResource CaptionTextBlockStyle}"
                                   Foreground="{ThemeResource TextFillColorTertiaryBrush}" />
                    </StackPanel>

                    <Button Grid.Column="2"
                            Style="{StaticResource GhostButtonStyle}"
                            Content="删除"
                            Command="{Binding DeleteCommand}" />
                </Grid>
            </Border>
        </DataTemplate>
    </ListView.ItemTemplate>
</ListView>
```

### 7.3 状态指示器

```xml
<!-- 加载状态 -->
<StackPanel Visibility="{x:Bind IsLoading, Mode=OneWay}">
    <ProgressRing IsActive="True" Width="32" Height="32" />
    <TextBlock Text="加载中..." Margin="0,8,0,0" />
</StackPanel>

<!-- 空状态 -->
<StackPanel Visibility="{x:Bind IsEmpty, Mode=OneWay}"
            HorizontalAlignment="Center"
            VerticalAlignment="Center">
    <SymbolIcon Symbol="Document" FontSize="48"
                Foreground="{ThemeResource TextFillColorTertiaryBrush}" />
    <TextBlock Text="暂无数据"
               Style="{StaticResource BodyTextBlockStyle}"
               Foreground="{ThemeResource TextFillColorTertiaryBrush}"
               Margin="0,12,0,0" />
</StackPanel>

<!-- 错误状态 -->
<StackPanel Visibility="{x:Bind HasError, Mode=OneWay}">
    <SymbolIcon Symbol="ErrorBadge" FontSize="48"
                Foreground="{ThemeResource SystemFillColorCriticalBrush}" />
    <TextBlock Text="{Binding ErrorMessage}"
               Foreground="{ThemeResource SystemFillColorCriticalBrush}"
               TextWrapping="Wrap"
               Margin="0,8,0,0" />
    <Button Content="重试"
            Style="{StaticResource SecondaryButtonStyle}"
            Command="{Binding RetryCommand}"
            Margin="0,12,0,0" />
</StackPanel>
```

---

## 八、参考资源

### 官方文档

- [XAML Theme Resources](https://learn.microsoft.com/windows/apps/develop/platform/xaml/xaml-theme-resources)
- [WinUI 3 Gallery](https://github.com/microsoft/WinUI-Gallery)
- [Microsoft.UI.Xaml 源码](https://github.com/microsoft/microsoft-ui-xaml)
- [Windows App SDK 文档](https://learn.microsoft.com/windows/apps/windows-app-sdk/)

### 相关工具

- **WinUI 3 Gallery 应用**: 在 Microsoft Store 搜索 "WinUI 3 Gallery" 下载官方示例应用
- **Visual Studio 2022**: 推荐使用最新版，支持 WinUI3 开发

### 颜色资源文件

| 文件名 | 路径 | 说明 |
|--------|------|------|
| `Common_themeresources_any.xaml` | WinUI 源码 | 所有主题颜色和画笔定义 |
| `TextBlock_themeresources.xaml` | WinUI 源码 | 排版样式定义 |
| `Generic.xaml` | Windows SDK | 默认控件模板 |

---

## 附录 A：颜色速查表

### 文本颜色

```
Light:  #E4000000 (Primary) → #5C000000 (Disabled)
Dark:   #FFFFFFFF (Primary) → #5DFFFFFF (Disabled)
```

### 背景颜色

```
Light:  #FFFFFF (Base) → #F9F9F9 (Tertiary)
Dark:   #202020 (Base) → #373737 (Senary)
```

### 状态颜色

```
Success: #107C10 (Light) / #6CCB5F (Dark)
Warning: #C19C00 (Light) / #FCE100 (Dark)
Error:   #D13438 (Light) / #FF99A4 (Dark)
```

---

*本文档将持续更新以反映最新的 WinUI3 设计规范。*
