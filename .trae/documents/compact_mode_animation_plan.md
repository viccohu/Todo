# 固定模式动画效果实现计划

## 需求分析
1. **大窗口变固定模式的动画**：从正常窗口大小平滑过渡到固定窗口大小
2. **固定模式收窄与展开的动画**：收窄/展开时平滑调整窗口高度

## 实现方案

### 方案说明
- 使用 `DispatcherTimer` 来实现窗口大小的平滑过渡动画
- 采用渐进式调整窗口尺寸，每帧调整一小部分
- 动画时长设为 250ms，帧率设为 60fps，每次调整步长约为 16.6ms

### 文件修改清单

1. **MainWindow.xaml.cs**
   - 添加动画相关字段和辅助方法
   - 修改 `EnterPinnedMode` - 添加动画
   - 修改 `ExitPinnedMode` - 添加动画  
   - 修改 `CompactMinimize_Click` - 添加收窄动画
   - 修改 `ExpandCompactWindow` - 添加展开动画

2. **WindowHelper.cs**
   - 可能需要修改 `ResizePinned` 方法以支持动画过程中的临时尺寸调整

## 具体实现步骤

### 1. 在 MainWindow.xaml.cs 中添加动画辅助字段和方法
- 添加动画计时器 `_animationTimer`
- 添加动画目标尺寸跟踪字段
- 添加 `AnimateWindowSize` 辅助方法，用于平滑调整窗口尺寸

### 2. 修改 EnterPinnedMode 方法
- 先隐藏内容，然后从正常尺寸动画缩小到固定尺寸
- 尺寸调整完成后显示紧凑模式内容

### 3. 修改 ExitPinnedMode 方法  
- 先隐藏紧凑模式内容，然后从固定尺寸动画放大到正常尺寸
- 尺寸调整完成后显示正常模式内容

### 4. 修改 CompactMinimize_Click 和 ExpandCompactWindow 方法
- 实现收窄动画（高度 500 → 90）
- 实现展开动画（高度 90 → 500）

## 预期效果
- 切换到/退出固定模式时有平滑的缩放动画
- 固定模式收窄/展开时有平滑的高度变化动画
- 整体体验流畅自然
