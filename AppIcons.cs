using Microsoft.UI.Xaml.Controls;
using System;

namespace Todo
{
    /// <summary>
    /// 图标管理 — SVG + ImageIcon，利用 SvgImageSource 原生缩放
    /// </summary>
    public static class AppIcons
    {
        private const string Base = "ms-appx:///Assets/icons/white/";

        // 导航
        public const string Notepad   = Base + "记事本.svg";
        public const string Important = Base + "重要任务.svg";
        public const string Recurring = Base + "例行任务.svg";

        // 例行任务子项
        public const string Daily     = Base + "日常.svg";
        public const string Weekly    = Base + "周常.svg";
        public const string Monthly   = Base + "月常.svg";

        // 重复选项
        public const string RecurDaily   = Base + "每天.svg";
        public const string RecurWeekly  = Base + "每周.svg";
        public const string RecurMonthly = Base + "每月.svg";
        public const string RecurYearly  = Base + "每年.svg";
        public const string RecurNone    = Base + "不循环.svg";

        // 列表 / 分组
        public const string TaskList  = Base + "任务列表.svg";
        public const string Group     = Base + "分组.svg";

        // 功能按钮
        public const string NewList   = Base + "新建任务列表.svg";
        public const string NewGroup  = Base + "新建分组.svg";
        public const string Completed = Base + "已完成.svg";
        public const string Calendar  = Base + "日历.svg";

        /// <summary>从 SVG 路径创建 ImageIcon</summary>
        public static ImageIcon Create(string path, int size = 16)
        {
            return new ImageIcon
            {
                Source = new Microsoft.UI.Xaml.Media.Imaging.SvgImageSource(new Uri(path)),
                Width = size,
                Height = size
            };
        }
    }
}
