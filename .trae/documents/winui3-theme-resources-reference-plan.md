# WinUI3 原生主题资源参考文档生成计划

## 目标
基于微软官方文档和 WinUI3 Gallery 源码，生成一份完整的 WinUI3 原生主题资源参考文档，便于日后开发 WinUI3 应用时快速查阅。

## 信息来源
1. **微软官方文档**: [XAML Theme Resources](https://learn.microsoft.com/windows/apps/develop/platform/xaml/xaml-theme-resources)
2. **WinUI3 Gallery GitHub**: https://github.com/microsoft/WinUI-Gallery
3. **Microsoft.UI.Xaml 源码**: 主题资源定义在 `Common_themeresources_any.xaml`

## 文档结构规划

### 1. 颜色系统 (Colors)
- **文本颜色** (Text Fill Colors)
  - Primary / Secondary / Tertiary
  - Disabled 状态
  
- **填充颜色** (Fill Colors)
  - Background / Control colors
  - Accent colors
  
- **边框颜色** (Border Colors)

### 2. 画笔资源 (Brush Resources)
- SolidColorBrush 资源
- AcrylicBrush 材质
- Mica/云母背景效果
- LinearGradientBrush / RadialGradientBrush

### 3. 排版系统 (Typography)
| 样式名称 | 字体重量 | 字号 |
|---------|---------|------|
| Caption | Regular | 12 |
| Body | Regular | 14 |
| Body Strong | Semibold | 14 |
| Body Large | Regular | 18 |
| Subtitle | Semibold | 20 |
| Title | Semibold | 28 |
| Title Large | Semibold | 40 |
| Display | Semibold | 68 |

### 4. 控件样式 (Control Styles)
- Button variants
- TextBox / Input controls
- ListView / GridView
- NavigationView
- Dialogs / Flyouts

### 5. 主题切换 (Theme Switching)
- Light / Dark / High Contrast
- ThemeResource vs StaticResource
- 运行时主题切换

## 输出文件
- 路径: `D:\Project\Todo\.trae\documents\winui3-theme-resources-reference.md`
- 格式: Markdown
- 语言: 中文（与用户消息一致）

## 实施步骤
1. 创建文档目录结构
2. 整理 WinUI3 颜色系统的 Light/Dark 主题 HEX 值
3. 编写 Brush 资源使用指南
4. 添加排版样式参考
5. 包含控件样式模板示例
6. 添加主题切换最佳实践

## 验证步骤
- 文档语法正确
- 代码示例可复制使用
- 颜色值与官方一致
