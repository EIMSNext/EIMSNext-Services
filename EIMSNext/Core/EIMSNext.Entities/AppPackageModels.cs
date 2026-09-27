namespace EIMSNext.Entities
{
    /// <summary>
    /// 可在环境间迁移的应用市场模板包。
    /// </summary>
    public class AppPackageManifest
    {
        /// <summary>当前支持的包格式版本。</summary>
        public const int CurrentFormatVersion = 1;

        /// <summary>包格式版本。</summary>
        public int FormatVersion { get; set; } = CurrentFormatVersion;

        /// <summary>应用市场档案。</summary>
        public AppProfile Profile { get; set; } = new();

        /// <summary>应用模板。</summary>
        public AppTemplate Template { get; set; } = new();

        /// <summary>表单模板集合。</summary>
        public List<FormTemplate> Forms { get; set; } = [];

        /// <summary>仪表盘模板集合。</summary>
        public List<DashboardTemplate> Dashboards { get; set; } = [];

        /// <summary>仪表盘项模板集合。</summary>
        public List<DashboardItemTemplate> DashboardItems { get; set; } = [];

        /// <summary>工作流模板集合。</summary>
        public List<WfDefinitionTemplate> Workflows { get; set; } = [];

        /// <summary>打印模板集合。</summary>
        public List<PrintDefTemplate> PrintDefinitions { get; set; } = [];

        /// <summary>授权组模板集合。</summary>
        public List<FormDataPermissionGroupTemplate> FormDataPermissionGroups { get; set; } = [];
    }

    /// <summary>单类模板资源的导入差异。</summary>
    public class AppPackageResourcePreview
    {
        /// <summary>资源类型。</summary>
        public string Resource { get; set; } = string.Empty;

        /// <summary>将新建的资源数量。</summary>
        public int CreateCount { get; set; }

        /// <summary>将更新的资源数量。</summary>
        public int UpdateCount { get; set; }

        /// <summary>将删除的资源数量。</summary>
        public int DeleteCount { get; set; }
    }

    /// <summary>应用模板包的导入预检结果。</summary>
    public class AppPackagePreview
    {
        /// <summary>包内应用市场档案 ID。</summary>
        public string AppProfileId { get; set; } = string.Empty;

        /// <summary>包内应用模板 ID。</summary>
        public string TemplateId { get; set; } = string.Empty;

        /// <summary>目标是否已存在同 ID 的应用市场档案。</summary>
        public bool ProfileExists { get; set; }

        /// <summary>档案动作，Create 或 Keep。</summary>
        public string ProfileAction { get; set; } = string.Empty;

        /// <summary>各模板资源的差异。</summary>
        public List<AppPackageResourcePreview> Resources { get; set; } = [];
    }

    /// <summary>应用模板包导入结果。</summary>
    public class AppPackageImportResult
    {
        /// <summary>目标应用市场档案 ID。</summary>
        public string AppProfileId { get; set; } = string.Empty;

        /// <summary>目标应用模板 ID。</summary>
        public string TemplateId { get; set; } = string.Empty;

        /// <summary>是否在目标新建了应用市场档案。</summary>
        public bool ProfileCreated { get; set; }
    }

    /// <summary>可下载的应用模板包。</summary>
    public class AppPackageExport
    {
        /// <summary>建议的下载文件名。</summary>
        public string FileName { get; set; } = string.Empty;

        /// <summary>ZIP 文件内容。</summary>
        public byte[] Content { get; set; } = [];
    }
}
