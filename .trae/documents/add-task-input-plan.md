# 添加任务输入框改进计划

## 目标
将添加任务按钮改为点击后显示输入框，回车键添加任务。

## 当前状态分析
- 添加任务是一个 Button，点击后直接添加名为"新任务"的任务项
- 没有输入框让用户直接输入任务内容
- 用户需要先添加任务，再修改标题

## 目标行为
1. 默认显示"添加任务"按钮
2. 点击按钮后，切换为输入框
3. 输入框自动获得焦点
4. 回车键：添加任务并恢复按钮状态
5. Escape 键：取消输入，恢复按钮状态
6. 失去焦点：如果内容不为空则添加任务，否则恢复按钮状态

## 实现方案

### XAML 修改
使用 Grid 容器包含两种状态，通过 Visibility 切换：

```xml
<Grid x:Name="AddTaskContainer">
    <!-- 按钮状态 -->
    <Border x:Name="AddTaskButton" ... Visibility="Visible">
        <Button Click="ShowAddTaskInput_Click">...</Button>
    </Border>
    
    <!-- 输入框状态 -->
    <Border x:Name="AddTaskInput" ... Visibility="Collapsed">
        <TextBox KeyDown="AddTaskInput_KeyDown" 
                 LostFocus="AddTaskInput_LostFocus"/>
    </Border>
</Grid>
```

### 代码后台修改
- `ShowAddTaskInput_Click`: 切换到输入框状态，设置焦点
- `AddTaskInput_KeyDown`: 处理回车和 Escape 键
- `AddTaskInput_LostFocus`: 处理失去焦点

## 文件变更清单

| 文件 | 操作 | 说明 |
|------|------|------|
| `MainWindow.xaml` | 修改 | 重构添加任务区域 |
| `MainWindow.xaml.cs` | 修改 | 添加输入框逻辑 |

## 验证步骤
1. 构建项目无错误
2. 点击按钮显示输入框
3. 输入内容后回车添加任务
4. Escape 取消输入
5. 失去焦点时正确处理
