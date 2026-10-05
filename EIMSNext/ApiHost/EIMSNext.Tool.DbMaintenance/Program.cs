using EIMSNext.Tool.DbMaintenance;

// 不带参数（典型场景：VS 里右键"启动"）或显式 -i/--interactive 时进入交互式菜单：
// 不用记命令行开关，也不会出现窗口一闪而过。
// 带了参数则按原样一次性执行，便于脚本/CI 调用。
return args.Any(x => x is "-i" or "--interactive") || args.Length == 0
    ? await MaintenanceMenu.RunAsync()
    : await MaintenanceRunner.ExecuteAsync(args);
