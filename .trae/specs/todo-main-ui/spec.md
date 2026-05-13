# Todo 应用主界面 - 产品需求文档

## Overview
- **Summary**: 根据用户提供的草图，构建一个 WinUI3 Todo 应用主界面，包含三栏布局：左侧导航面板、中间任务列表、右侧详情抽屉。
- **Purpose**: 提供一个符合 Windows 11 设计规范的现代化 Todo 应用界面，使用 WinUI3 原生主题资源。
- **Target Users**: 需要高效管理日常任务的 Windows 用户。

## Goals
- 实现三栏布局：左侧导航、中间任务列表、右侧抽屉详情面板
- 使用 WinUI3 原生主题资源（颜色、画笔、排版）
- 实现响应式设计，支持窗口大小调整
- 实现抽屉面板的展开/收起动画效果
- 界面布局与草图保持一致

## Non-Goals (Out of Scope)
- 不实现业务逻辑（数据持久化、任务状态管理）
- 不实现后端 API 集成
- 不实现用户认证功能
- 不实现复杂动画效果（除抽屉展开/收起）

## Background & Context
- 当前项目是一个 WinUI3 桌面应用项目（.NET 8）
- 需要遵循 Fluent Design System 设计规范
- 参考文档：[WinUI3 主题资源参考文档](file:///D:/Project/Todo/.trae/documents/winui3-theme-resources-reference.md)

## Functional Requirements
- **FR-1**: 左侧导航面板包含任务日历、重要任务、常驻任务（日常/周常/月常）、自定义任务区
- **FR-2**: 中间主内容区显示当前选中分类的任务列表，包含待完成和已完成任务
- **FR-3**: 右侧抽屉面板在点击任务项时展开，显示任务拆分、备注和提醒设置
- **FR-4**: 底部包含添加任务按钮和导航操作按钮

## Non-Functional Requirements
- **NFR-1**: 使用 WinUI3 原生主题资源，支持浅色/深色主题切换
- **NFR-2**: 使用 MicaBackdrop 作为窗口背景
- **NFR-3**: 界面元素间距和尺寸遵循 Fluent Design 规范

## Constraints
- **Technical**: WinUI3 + .NET 8，Windows 10+ 目标平台
- **Dependencies**: Microsoft.WindowsAppSDK 2.0.1

## Assumptions
- 用户使用 Windows 11 操作系统（推荐）
- 应用默认使用深色主题
- 任务数据为模拟数据（静态展示）

## Acceptance Criteria

### AC-1: 左侧导航面板布局正确
- **Given**: 应用启动后
- **When**: 用户查看左侧面板
- **Then**: 显示任务日历、重要任务（选中状态）、常驻任务（含展开项）、自定义任务区
- **Verification**: `human-judgment`

### AC-2: 中间任务列表布局正确
- **Given**: 应用启动后
- **When**: 用户查看中间区域
- **Then**: 显示标题"重要任务"、待完成任务列表、已完成任务列表、添加任务按钮
- **Verification**: `human-judgment`

### AC-3: 右侧抽屉面板布局正确
- **Given**: 点击某个任务项后
- **When**: 抽屉面板展开
- **Then**: 显示任务拆分列表、添加备注文本框、提醒设置区域
- **Verification**: `human-judgment`

### AC-4: 使用 WinUI3 主题资源
- **Given**: 应用运行中
- **When**: 切换系统主题（浅/深色）
- **Then**: 界面颜色自动跟随主题变化
- **Verification**: `human-judgment`

### AC-5: Mica 背景效果
- **Given**: 应用启动后
- **When**: 查看窗口背景
- **Then**: 显示 MicaBackdrop 模糊效果
- **Verification**: `human-judgment`

## Open Questions
- [ ] 是否需要支持窗口最小化到托盘？
- [ ] 是否需要支持拖拽排序任务？
