﻿using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

using EIMSNext.Core.Entity;
using EIMSNext.Core.Query;

namespace EIMSNext.Entity
{
    /// <summary>
    /// 表单定义
    /// </summary>
    public class FormDef : CorpEntityBase
    {
        /// <summary>
        /// 
        /// </summary>
        public string AppId { get; set; } = string.Empty;

        /// <summary>
        /// 模板Id, 对于从模板安装的表单
        /// </summary>
        public string? TemplateId { get; set; }

        /// <summary>
        /// 
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// 
        /// </summary>
        public FormType Type { get; set; } = FormType.Form;

        /// <summary>
        /// 
        /// </summary>
        public FormContent Content { get; set; } = new FormContent();

        /// <summary>
        /// 是否台账， 台账不支持手动增删改？？？
        /// </summary>
        public bool IsLedger { get; set; }

        /// <summary>
        /// 是否流程表单
        /// </summary>
        public bool UsingWorkflow { get; set; }
    }

    /// <summary>
    /// 自定义表单类型
    /// </summary>
    public enum FormType
    {
        /// <summary>
        /// 表单
        /// </summary>
        Form,
        /// <summary>
        /// 仪表盘
        /// </summary>
        Dashboard,
        /// <summary>
        /// 表单分组
        /// </summary>
        Group
    }

    /// <summary>
    /// 表单定义内容，包括修改历史等
    /// </summary>
    public class FormContent
    {
        /// <summary>
        /// 表单布局
        /// </summary>
        public string Layout { get; set; } = string.Empty;
        /// <summary>
        /// 表单设置
        /// </summary>
        public string Options { get; set; } = string.Empty;
        /// <summary>
        /// 表单组件（仅表单元素，不包含布局组件）
        /// </summary>
        public IList<FieldDef>? Items { get; set; }
    }

    /// <summary>
    /// 
    /// </summary>
    public class FieldDef
    {
        /// <summary>
        /// 
        /// </summary>
        public string Field { get; set; } = string.Empty;
        /// <summary>
        /// 
        /// </summary>
        public string Type { get; set; } = FieldType.Input;
        /// <summary>
        /// 
        /// </summary>
        public string Title { get; set; } = string.Empty;
        /// <summary>
        /// 
        /// </summary>
        public FieldOpt Options { get; set; } = new FieldOpt();

        /// <summary>
        /// 子表单中的列
        /// </summary>
        public IList<FieldDef>? Columns { get; set; }
    }

    /// <summary>
    /// 
    /// </summary>
    public class FieldOpt
    {
        /// <summary>
        /// 
        /// </summary>
        public string? Format { get; set; }
        /// <summary>
        /// 值配置
        /// </summary>
        public ValueOpt? ValueOpt { get; set; }
    }
    /// <summary>
    /// 
    /// </summary>
    public class ValueOpt
    {
        /// <summary>
        /// 值公式
        /// </summary>
        public string? Formula { get; set; }
        /// <summary>
        /// 公式依赖
        /// </summary>
        public string? Depends { get; set; }
    }

    /// <summary>
    /// 
    /// </summary>
    public class FieldChangeLog
    {
        /// <summary>
        /// 
        /// </summary>
        public ChangeType ChangeType { get; set; }
        /// <summary>
        /// 
        /// </summary>
        public DateTime ChangeTime { get; set; }
        /// <summary>
        /// 
        /// </summary>
        public FieldDef Field { get; set; } = new FieldDef();
        /// <summary>
        /// 
        /// </summary>
        public Operator ChangedBy { get; set; } = Operator.Empty;
    }

    /// <summary>
    /// 变动类型
    /// </summary>
    public enum ChangeType
    {
        /// <summary>
        /// 
        /// </summary>
        Remove,
        /// <summary>
        /// 
        /// </summary>
        Restore
    }
}
