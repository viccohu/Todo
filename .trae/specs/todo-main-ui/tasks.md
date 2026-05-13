# Todo 应用主界面 - 实现计划

## [x] Task 1: 创建左侧导航面板
- **Priority**: P0
- **Depends On**: None
- **Description**: 
  - 实现左侧导航面板布局
  - 包含：任务日历、重要任务（选中状态）、常驻任务（日常/周常/月常展开项）、自定义任务区
  - 底部：新建任务列表、创建分组按钮
- **Acceptance Criteria Addressed**: AC-1
- **Test Requirements**:
  - `human-judgement` TR-1.1: 左侧面板显示完整的导航项，包含所有草图中的元素
  - `human-judgement` TR-1.2: 重要任务项显示选中状态（蓝色指示条）
- **Notes**: 使用 NavigationView 或自定义 StackPanel 实现

## [x] Task 2: 创建中间任务列表区域
- **Priority**: P0
- **Depends On**: Task 1
- **Description**: 
  - 实现中间主内容区布局
  - 包含：标题"重要任务"、待完成任务列表、已完成任务区域、添加任务按钮
  - 任务项包含复选框、任务名称、截止日期
- **Acceptance Criteria Addressed**: AC-2
- **Test Requirements**:
  - `human-judgement` TR-2.1: 中间区域显示标题和任务列表
  - `human-judgement` TR-2.2: 任务项包含复选框、名称、日期信息
  - `human-judgement` TR-2.3: 已完成任务显示勾选状态和不同样式
- **Notes**: 使用 ListView 或 ItemsRepeater 实现任务列表

## [x] Task 3: 创建右侧抽屉面板
- **Priority**: P0
- **Depends On**: Task 2
- **Description**: 
  - 实现右侧抽屉面板布局
  - 包含：任务拆分列表、添加备注文本框、提醒设置区域
  - 实现抽屉展开/收起动画效果
- **Acceptance Criteria Addressed**: AC-3
- **Test Requirements**:
  - `human-judgement` TR-3.1: 抽屉面板包含任务拆分、备注、提醒三个区域
  - `human-judgement` TR-3.2: 点击任务项时抽屉展开，再次点击或选择其他任务时更新内容
- **Notes**: 使用 NavigationView.Pane 或自定义 Panel 实现抽屉

## [x] Task 4: 应用 WinUI3 主题资源
- **Priority**: P1
- **Depends On**: Task 1, Task 2, Task 3
- **Description**: 
  - 应用 WinUI3 原生颜色资源（TextFillColor、ControlFillColor、ControlStrokeColor）
  - 应用 WinUI3 排版样式（BodyTextBlockStyle、SubtitleTextBlockStyle）
  - 配置 MicaBackdrop 背景
- **Acceptance Criteria Addressed**: AC-4, AC-5
- **Test Requirements**:
  - `human-judgement` TR-4.1: 使用 ThemeResource 引用颜色资源
  - `human-judgement` TR-4.2: 窗口显示 MicaBackdrop 效果
- **Notes**: 参考主题资源参考文档

## [ ] Task 5: 整合三栏布局
- **Priority**: P1
- **Depends On**: Task 1, Task 2, Task 3, Task 4
- **Description**: 
  - 使用 Grid 布局整合三栏
  - 设置合理的列宽和响应式行为
  - 确保各区域正确对齐和间距
- **Acceptance Criteria Addressed**: AC-1, AC-2, AC-3
- **Test Requirements**:
  - `human-judgement` TR-5.1: 三栏布局正确显示
  - `human-judgement` TR-5.2: 窗口调整大小时布局自适应
- **Notes**: 使用 Grid.ColumnDefinitions 定义三栏布局

## [x] Task 6: 添加模拟数据
- **Priority**: P2
- **Depends On**: Task 2, Task 3
- **Description**: 
  - 添加模拟任务数据用于界面展示
  - 包含待完成和已完成任务示例
  - 添加任务拆分示例数据
- **Acceptance Criteria Addressed**: AC-2, AC-3
- **Test Requirements**:
  - `human-judgement` TR-6.1: 界面显示模拟数据，无需业务逻辑
- **Notes**: 使用 ObservableCollection 绑定模拟数据
