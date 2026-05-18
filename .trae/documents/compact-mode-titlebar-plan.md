# 固定模式标题栏和折叠按钮修复计划

## 问题分析

1. **固定模式下原本程序的标题栏还在没有隐藏** - `AppTitleBar` 在固定模式下仍然显示
2. **收起与展开按钮需要独占一行宽度** - 当前按钮在紧凑内容区的标题 Grid 中，没有独占一行

## 实现步骤

### 1. 修改 MainWindow.xaml.cs - 进入/退出固定模式时控制标题栏显示

- 在 `EnterPinnedMode()` 中添加 `AppTitleBar.Visibility = Visibility.Collapsed`
- 在 `ExitPinnedMode()` 中添加 `AppTitleBar.Visibility = Visibility.Visible`

### 2. 修改 MainWindow.xaml - 重构紧凑模式标题栏布局

**当前结构（有问题的）：**
```
CompactContent (Grid)
├── DummyTitleBar
└── Grid (Padding="16,8,16,16")
    └── Grid (Row 0 - 标题栏)
        ├── TextBlock "T O  D O" (居中)
        └── StackPanel (右侧按钮)
            ├── ExpandButton
            └── 恢复正常窗口按钮
```

**修改后的结构：**
```
CompactContent (Grid)
├── DummyTitleBar (Height=0)
└── Grid (Padding="16,8,16,16")
    ├── Grid (Row 0 - 标题栏，高度48)
    │   ├── Grid.ColumnDefinitions: *, Auto
    │   ├── TextBlock "T O  D O" (Column=0, 居中)
    │   └── 恢复正常窗口按钮 (Column=1)
    ├── Grid (Row 1 - 展开按钮，独立一行)
    │   └── Button "展开" (HorizontalAlignment=Stretch)
    ├── ScrollViewer (Row 2 - 任务列表)
    └── Button (Row 3 - 底部收起按钮)
```

### 3. 修改 MainWindow.xaml.cs - 更新折叠逻辑

- `CompactMinimize_Click()` - 收起时隐藏任务列表和底部按钮，显示展开按钮行
- `ExpandCompactWindow()` - 展开时隐藏展开按钮行，显示任务列表和底部按钮

## 文件修改清单

| 文件 | 修改内容 |
|------|----------|
| MainWindow.xaml | 重构紧凑模式标题栏和折叠按钮布局 |
| MainWindow.xaml.cs | 隐藏/显示 AppTitleBar，更新折叠逻辑 |

## 预期效果

- 固定模式下隐藏原本的标题栏
- 紧凑模式有独立的标题栏（标题 + 恢复正常窗口按钮）
- 展开/收起按钮独占一行宽度