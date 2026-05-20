# Todo 项目开发规则

## 技术栈
- **框架**: WinUI3 + .NET 8
- **目标平台**: Windows 10+ (推荐 Windows 11)
- **依赖**: Microsoft.WindowsAppSDK 2.0.1

## 重要注意事项
- 必须查询官方文档，以最佳实践进行开发。

## 设计规范

### 主题和颜色
- 必须使用 WinUI3 原生主题资源 (`ThemeResource`)
- 支持浅色/深色主题自动切换
- 使用系统颜色资源：
  - `SystemAccentColor` - 强调色
  - `SystemBaseHighColor` - 基础高亮色
  - `SystemBaseLowColor` - 基础低亮色
  - `SystemBaseMediumColor` - 基础中等色

### 背景
- 窗口背景使用 `MicaBackdrop`
- 面板背景使用适当的透明度

### 排版
- 使用 WinUI3 预定义样式：
  - `TitleTextBlockStyle` - 标题
  - `SubtitleTextBlockStyle` - 副标题
  - `BodyTextBlockStyle` - 正文
  - `CaptionTextBlockStyle` - 说明文字

### 布局
- 三栏布局：左侧导航(240px) + 中间内容(自适应) + 右侧抽屉(320px)
- 响应式设计支持窗口大小调整
- 遵循 Fluent Design 间距规范（8px 基础单位）

## 代码规范

### 命名约定
- 类名: PascalCase (如 `TaskList`, `DatabaseService`)
- 方法名: PascalCase
- 属性名: PascalCase
- 私有字段: _camelCase
- 常量: PascalCase 或全大写

### 文件组织
```
Models/          - 数据模型类
Services/        - 业务服务类
Pages/           - 页面 XAML 和代码后台
Controls/        - 自定义控件
Assets/          - 静态资源
```

### MVVM 模式
- 视图 (XAML) 与逻辑分离
- 使用 `x:Bind` 进行数据绑定
- 实现 `INotifyPropertyChanged` 接口

## 测试验证

### UI 验证
- [ ] 界面元素正确显示
- [ ] 主题切换正常工作
- [ ] 响应式布局正常
- [ ] 动画效果流畅

### 功能验证
- [ ] 数据操作正确
- [ ] 状态管理正确
- [ ] 错误处理完善

## 禁止事项
- 禁止使用非winui3规范的代码或函数、api
- 不要在视图层直接操作数据库
- 不要忽略异常处理
- 不要在主线程执行耗时操作
