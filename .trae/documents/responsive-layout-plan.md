# 任务项响应式布局计划

## 目标
实现任务项（包括添加项）响应式按窗口宽度伸展，抽屉展开时挤压任务项宽度。

## 当前状态分析
- 任务列表使用 `StackPanel` + `MinWidth="640"` + `HorizontalAlignment="Center"`，限制了宽度伸展
- 添加任务按钮使用 `MaxWidth="640"`，也限制了宽度
- 抽屉使用 Grid 两列布局，但 DrawerColumn 的 Width 切换方式会导致抽屉覆盖内容而非挤压

## 问题根源
当前布局：
```xml
<Grid.ColumnDefinitions>
    <ColumnDefinition Width="*"/>           <!-- 主内容区 -->
    <ColumnDefinition Width="0"/>           <!-- 抽屉区 -->
</Grid.ColumnDefinitions>
```
- 抽屉展开时设置 `Width="320"`，但主内容区仍是 `Width="*"`，导致总宽度增加
- 任务项有固定宽度限制，不会随窗口伸展

## 解决方案

### 1. 修改任务项布局
- 移除 `MinWidth="640"` 和 `MaxWidth="640"` 限制
- 使用 `HorizontalAlignment="Stretch"` 让任务项伸展
- 设置合理的 `MinWidth` 防止过窄

### 2. 修改抽屉布局方式
使用 `Grid` 的比例布局实现挤压效果：
```xml
<Grid.ColumnDefinitions>
    <ColumnDefinition Width="*" x:Name="ContentColumn"/>      <!-- 主内容区 -->
    <ColumnDefinition Width="Auto" x:Name="DrawerColumn"/>    <!-- 抽屉区 -->
</Grid.ColumnDefinitions>
```
- 抽屉展开时设置 `Width="320"`，主内容区自动收缩
- 抽屉收起时设置 `Width="0"`，主内容区自动伸展

### 3. 添加任务按钮同步响应
- 移除 `MaxWidth` 限制
- 使用 `HorizontalAlignment="Stretch"` 伸展

## 文件变更清单

| 文件 | 操作 | 说明 |
|------|------|------|
| `MainWindow.xaml` | 修改 | 调整布局属性 |

## 具体修改

### MainWindow.xaml 修改点：

1. **任务列表 StackPanel**：
   - `MinWidth="640"` → `MinWidth="300"`
   - `HorizontalAlignment="Center"` → `HorizontalAlignment="Stretch"`

2. **添加任务 Border**：
   - 移除 `MaxWidth="640"`
   - 确保 `HorizontalAlignment="Stretch"`

3. **抽屉 Grid 列定义**：
   - 主内容区保持 `Width="*"`
   - 抽屉区改为 `Width="Auto"`

4. **抽屉 DetailDrawer**：
   - 添加 `Width="320"` 固定宽度

## 验证步骤
1. 构建项目无错误
2. 任务项随窗口宽度伸展
3. 抽屉展开时任务项宽度收窄
4. 抽屉收起时任务项宽度伸展
