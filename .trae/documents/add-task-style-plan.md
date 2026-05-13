# 添加任务输入框样式优化计划

## 目标
优化添加任务输入框的样式，使其更简洁并与按钮状态保持一致。

## 当前状态分析
- 输入框状态有蓝色边框 (`BorderBrush="#0078d4"`)
- 有占位符文本 (`PlaceholderText="输入任务内容，按回车添加"`)
- 输入框没有显示前面的 icon
- 输入框的 Padding 与按钮中的文字位置不对齐

## 目标效果
1. 输入框不要有任何特殊样式，与按钮状态外观一致
2. 移除占位符文本
3. 保留前面的 icon（加号图标）
4. 输入框对齐原本"添加任务"文字的位置

## 实现方案

### XAML 修改
将输入框状态改为与按钮状态相同的结构：
- Border 样式与按钮状态一致（灰色边框、圆角）
- 内部使用 StackPanel 包含 icon 和 TextBox
- TextBox 移除 PlaceholderText
- TextBox 的 Padding 调整以对齐文字位置

```xml
<!-- 输入框状态 -->
<Border x:Name="AddTaskInputArea" 
        BorderBrush="#333" 
        BorderThickness="1" 
        CornerRadius="8" 
        Height="60"
        HorizontalAlignment="Stretch"
        Background="#1e1e1e"
        Visibility="Collapsed">
    <StackPanel Orientation="Horizontal" Spacing="12" Margin="10,0">
        <FontIcon Glyph="&#xE710;" FontSize="18" Foreground="#0078d4"/>
        <TextBox x:Name="AddTaskTextBox" 
                 Background="Transparent" 
                 BorderThickness="0"
                 FontSize="14"
                 Foreground="#0078d4"
                 VerticalAlignment="Center"
                 Width="Auto"
                 KeyDown="AddTaskInput_KeyDown"
                 LostFocus="AddTaskInput_LostFocus"/>
    </StackPanel>
</Border>
```

## 文件变更清单

| 文件 | 操作 | 说明 |
|------|------|------|
| `MainWindow.xaml` | 修改 | 优化输入框样式 |

## 验证步骤
1. 构建项目无错误
2. 点击按钮后输入框无蓝色边框
3. 输入框前面显示加号 icon
4. 输入位置对齐原本"添加任务"文字
5. 无占位符文本
