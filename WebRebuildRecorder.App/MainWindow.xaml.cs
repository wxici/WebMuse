using System.ComponentModel;
using System.Diagnostics;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using Microsoft.Web.WebView2.Core;
using WebRebuildRecorder.App.Core.Serialization;
using WebRebuildRecorder.App.Models;
using WebRebuildRecorder.App.Services;
using WebRebuildRecorder.App.ViewModels;
using WebRebuildRecorder.App.Views;
using WinForms = System.Windows.Forms;

namespace WebRebuildRecorder.App;

public partial class MainWindow : Window
{
    private readonly AppLogger _logger;
    private readonly AppSettingsService _appSettingsService;
    private readonly ProjectService _projectService;
    private readonly SourceSnapshotService _sourceSnapshotService;
    private readonly FfmpegScreenRecorder _recorder;
    private readonly FfprobeService _ffprobeService;
    private readonly FfmpegFrameExtractor _frameExtractor;
    private readonly ZipPackageService _zipPackageService;
    private readonly AiSiteWorkflowService _aiSiteWorkflowService;
    private readonly DomInteractionTargetCollector _domTargetCollector;
    private readonly UserIntentAssetService _userIntentAssetService;
    private readonly TemplateObservationMarkdownGenerator _markdownGenerator;
    private readonly ActionLogger _actionLogger;
    private readonly MarkerService _markerService;
    private readonly ScreenCaptureService _screenCaptureService;
    private readonly RegionSelectionService _regionSelectionService;
    private readonly MainWindowViewModel _viewModel;
    private readonly DispatcherTimer _recordingTimer;
    private readonly DispatcherTimer _manualMouseDetectionTimer;
    private readonly DispatcherTimer _frameProgressTimer;
    private readonly DispatcherTimer _manualPromptHideTimer;

    private HotKeyService? _hotKeyService;
    private RebuildProject? _project;
    private WorkflowState _workflowState = WorkflowState.Idle;
    private RecordingArea _selectedArea = RecordingArea.FullScreen();
    private RecordingInfo? _recordingInfo;
    private FrameExtractResult? _frameResult;
    private FrameExtractOptions? _frameOptions;
    private IBrowserController? _browserController;
    private CancellationTokenSource? _autoObserveCancellation;
    private Task? _autoObserveTask;
    private FloatingRecorderWindow? _floatingWindow;
    private CompletionWindow? _completionWindow;
    private DetachedPreviewWindow? _detachedPreviewWindow;
    private AppSettings _appSettings = new();
    private DateTime _recordingStart;
    private DateTime _recordingEnd;
    private DateTime? _openUrlTime;
    private DateTime _lastMousePromptTime = DateTime.MinValue;
    private ScreenPoint _lastObservedMousePosition;
    private int _manualMouseAnomalyCount;
    private bool _hadManualTakeover;
    private bool _websiteOpened;
    private bool _automaticPipelineRunning;
    private bool _currentRecordingIsAutoObserve;
    private bool _currentRecordingStartedBeforeOpenUrl;
    private bool _embeddedPreviewInitialized;
    private RecordingStartMode _currentRecordingStartMode = RecordingStartMode.RecordBeforeOpenUrl;
    private int _manualScreenshotCounter;
    private int _estimatedFrameCount;
    private string? _activeExtractionDirectory;
    private string? _observationMarkdownPath;
    private string? _framesZipPath;
    private string? _observationPackagePath;
    private ChatGptPackageResult? _chatGptPackageResult;
    private CodexPackageResult? _codexPackageResult;
    private AssetRequirementReport? _assetRequirementReport;
    private SourceSnapshotResult? _lastSourceSnapshotResult;
    private string? _currentRunId;
    private string? _lastEmbeddedPreviewUri;
    private bool _useFallbackForCodex;
    private ActionProfile _activeActionProfile = new();
    private readonly List<HotKeyRegistrationFailure> _hotkeyFailures = [];

    private const string SourceSnapshotRenderedEvidenceScript = """
(() => {
  const clean = (value, max = 500) => (value || '').toString().replace(/\s+/g, ' ').trim().slice(0, max);
  const rectOf = (el) => {
    const r = el.getBoundingClientRect();
    return {
      x: Math.round(r.x),
      y: Math.round(r.y),
      width: Math.round(r.width),
      height: Math.round(r.height)
    };
  };
  const selectorOf = (el) => {
    if (el.id) return '#' + el.id;
    if (el.className && typeof el.className === 'string') {
      return el.tagName.toLowerCase() + '.' + el.className.trim().split(/\s+/).slice(0, 2).join('.');
    }
    return el.tagName.toLowerCase();
  };
  const candidates = Array.from(document.querySelectorAll(
    'header,nav,main,section,article,footer,h1,h2,h3,p,a,button,img,[role="button"],[class*="hero"],[class*="card"]'
  )).slice(0, 160);

  const elements = candidates.map(el => {
    const r = rectOf(el);
    return {
      tag: el.tagName.toLowerCase(),
      id: el.id || '',
      className: typeof el.className === 'string' ? el.className : '',
      text: clean(el.innerText || el.alt || el.getAttribute('aria-label') || '', 240),
      href: el.href || '',
      src: el.currentSrc || el.src || '',
      x: r.x,
      y: r.y,
      width: r.width,
      height: r.height
    };
  });

  const styleSamples = candidates.slice(0, 60).map(el => {
    const cs = window.getComputedStyle(el);
    return {
      selector: selectorOf(el),
      color: cs.color || '',
      backgroundColor: cs.backgroundColor || '',
      fontFamily: cs.fontFamily || '',
      fontSize: cs.fontSize || '',
      fontWeight: cs.fontWeight || ''
    };
  });

  return {
    renderSucceeded: true,
    renderError: '',
    domHtml: document.documentElement.outerHTML.slice(0, 1000000),
    visibleText: (document.body ? document.body.innerText : '').slice(0, 50000),
    viewport: {
      width: window.innerWidth,
      height: window.innerHeight,
      devicePixelRatio: window.devicePixelRatio || 1,
      scrollHeight: document.documentElement.scrollHeight || document.body.scrollHeight || 0
    },
    elements,
    styleSamples
  };
})()
""";

    public MainWindow()
    {
        InitializeComponent();

        _logger = new AppLogger();
        _appSettingsService = new AppSettingsService();
        _projectService = new ProjectService(_logger);
        _sourceSnapshotService = new SourceSnapshotService(_logger);
        _recorder = new FfmpegScreenRecorder(_logger);
        _ffprobeService = new FfprobeService(_logger);
        _frameExtractor = new FfmpegFrameExtractor(_logger);
        _zipPackageService = new ZipPackageService(_logger);
        _aiSiteWorkflowService = new AiSiteWorkflowService(_logger);
        _domTargetCollector = new DomInteractionTargetCollector(_logger);
        _userIntentAssetService = new UserIntentAssetService(_logger);
        _markdownGenerator = new TemplateObservationMarkdownGenerator(_logger);
        _actionLogger = new ActionLogger(_logger);
        _markerService = new MarkerService(_logger);
        _screenCaptureService = new ScreenCaptureService(_logger);
        _regionSelectionService = new RegionSelectionService();
        _viewModel = new MainWindowViewModel();
        DataContext = _viewModel;

        _recordingTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(200) };
        _recordingTimer.Tick += RecordingTimer_Tick;
        _manualMouseDetectionTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(350) };
        _manualMouseDetectionTimer.Tick += ManualMouseDetectionTimer_Tick;
        _frameProgressTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(700) };
        _frameProgressTimer.Tick += FrameProgressTimer_Tick;
        _manualPromptHideTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
        _manualPromptHideTimer.Tick += (_, _) =>
        {
            _manualPromptHideTimer.Stop();
            _floatingWindow?.HideManualTakeoverPrompt();
        };

        _logger.LineWritten += Logger_LineWritten;
        OutputRootBox.Text = _projectService.GetFallbackRootDirectory();
        FfmpegPathBox.Text = ToolPathResolver.GetDefaultFfmpegPath();
        FfprobePathBox.Text = ToolPathResolver.GetDefaultFfprobePath();
        UpdateButtonStates();
    }

    private async void Window_Loaded(object sender, RoutedEventArgs e)
    {
        await SafeRunAsync(LoadAppSettingsAsync);
        await SafeRunAsync(RefreshRecentProjectsAsync);

        _hotKeyService = new HotKeyService();
        _hotKeyService.HotKeyPressed += HotKeyService_HotKeyPressed;
        _hotKeyService.RegisterFailed += (_, failure) =>
        {
            _hotkeyFailures.Add(failure);
            _logger.Warn(failure.ToString());
            UpdateHotkeyText();
        };
        _hotKeyService.Register(this, _appSettings.Hotkeys);
        UpdateHotkeyText();
        _logger.Info(_appSettings.Hotkeys.Enabled
            ? "全局快捷键注册完成。"
            : "已根据设置关闭全局快捷键。");
    }

    private async void Window_Closing(object? sender, CancelEventArgs e)
    {
        if (_recorder.IsRecording)
        {
            e.Cancel = true;
            await SafeRunAsync(async () =>
            {
                await StopRecordingAsync();
                CloseDetachedPreviewWindow();
                Closing -= Window_Closing;
                Close();
            });
            return;
        }

        _hotKeyService?.UnregisterAll();
        _hotKeyService?.Dispose();
        _floatingWindow?.Close();
        _completionWindow?.Close();
        CloseDetachedPreviewWindow();
        _autoObserveCancellation?.Cancel();
        await SafeRunAsync(SaveAppSettingsAsync);
    }

    private async void NewProjectButton_Click(object sender, RoutedEventArgs e)
    {
        await SafeRunAsync(CreateProjectAsync);
    }

    private async void OpenWebsiteButton_Click(object sender, RoutedEventArgs e)
    {
        await SafeRunAsync(OpenWebsiteAsync);
    }

    private async void PreviewReferenceInWebViewButton_Click(object sender, RoutedEventArgs e)
    {
        await SafeRunAsync(PreviewReferenceInWebViewAsync);
    }

    private async void PreviewOutputSiteInWebViewButton_Click(object sender, RoutedEventArgs e)
    {
        await SafeRunAsync(PreviewOutputSiteInWebViewAsync);
    }

    private async void RefreshEmbeddedPreviewButton_Click(object sender, RoutedEventArgs e)
    {
        await SafeRunAsync(RefreshEmbeddedPreviewAsync);
    }

    private void OpenPreviewInExternalBrowserButton_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_lastEmbeddedPreviewUri))
        {
            _logger.Warn("当前没有可外部打开的内嵌预览地址。");
            EmbeddedPreviewStatusText.Text = "WebView2 预览：当前没有可外部打开的地址。";
            return;
        }

        Process.Start(new ProcessStartInfo
        {
            FileName = _lastEmbeddedPreviewUri,
            UseShellExecute = true
        });
    }

    private async void OpenDetachedPreviewWindowButton_Click(object sender, RoutedEventArgs e)
    {
        await SafeRunAsync(OpenDetachedPreviewWindowAsync);
    }

    private async void GenerateSourceSnapshotButton_Click(object sender, RoutedEventArgs e)
    {
        await SafeRunAsync(RunSourceSnapshotAsync);
    }

    private void OpenSourceSnapshotDirectoryButton_Click(object sender, RoutedEventArgs e)
    {
        if (_project is null)
        {
            return;
        }

        var directory = Path.Combine(_project.ProjectDirectory, "source-snapshot");
        if (!Directory.Exists(directory))
        {
            SourceSnapshotStatusText.Text = "Source Snapshot：快照目录尚不存在。";
            return;
        }

        Process.Start(new ProcessStartInfo
        {
            FileName = directory,
            UseShellExecute = true
        });
    }

    private async void StartRecordingButton_Click(object sender, RoutedEventArgs e)
    {
        await SafeRunAsync(StartRecordingAsync);
    }

    private async void AutoObserveRecordingButton_Click(object sender, RoutedEventArgs e)
    {
        await SafeRunAsync(StartAutomaticClosedLoopAsync);
    }

    private async void CloseProjectButton_Click(object sender, RoutedEventArgs e)
    {
        await SafeRunAsync(CloseCurrentProjectAsync);
    }

    private async void OpenRecentProjectButton_Click(object sender, RoutedEventArgs e)
    {
        await SafeRunAsync(OpenSelectedRecentProjectAsync);
    }

    private async void StartupRecentProjectsListBox_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        await SafeRunAsync(OpenSelectedRecentProjectAsync);
    }

    private async void OpenExistingProjectDirectoryButton_Click(object sender, RoutedEventArgs e)
    {
        await SafeRunAsync(OpenExistingProjectDirectoryAsync);
    }

    private async void RemoveRecentProjectButton_Click(object sender, RoutedEventArgs e)
    {
        await SafeRunAsync(RemoveSelectedRecentProjectAsync);
    }

    private void OpenRecentProjectDirectoryButton_Click(object sender, RoutedEventArgs e)
    {
        if (GetSelectedRecentProject() is ProjectHistoryItem item)
        {
            OpenDirectory(item.ProjectDirectory);
        }
    }

    private async void StopRecordingButton_Click(object sender, RoutedEventArgs e)
    {
        await SafeRunAsync(StopRecordingAsync);
    }

    private async void ExtractFramesButton_Click(object sender, RoutedEventArgs e)
    {
        await SafeRunAsync(ExtractFramesAsync);
    }

    private async void PackageButton_Click(object sender, RoutedEventArgs e)
    {
        await SafeRunAsync(PackageAsync);
    }

    private async void GenerateChatGptPackageButton_Click(object sender, RoutedEventArgs e)
    {
        await SafeRunAsync(GenerateChatGptPackageAsync);
    }

    private async void ImportAssetsWorkflowButton_Click(object sender, RoutedEventArgs e)
    {
        await SafeRunAsync(ImportAssetsAsync);
    }

    private void OpenAssetLibraryButton_Click(object sender, RoutedEventArgs e)
    {
        if (_project is not null)
        {
            OpenDirectory(_project.SourceAssetsDirectory);
        }
    }

    private async void PasteFavoriteImageButton_Click(object sender, RoutedEventArgs e)
    {
        await SafeRunAsync(() => PasteUserIntentImageAsync(UserIntentFieldNames.FavoriteParts));
    }

    private async void UploadFavoriteImageButton_Click(object sender, RoutedEventArgs e)
    {
        await SafeRunAsync(() => UploadUserIntentImagesAsync(UserIntentFieldNames.FavoriteParts));
    }

    private void OpenFavoriteImageDirectoryButton_Click(object sender, RoutedEventArgs e)
    {
        OpenUserIntentImageDirectory(UserIntentFieldNames.FavoriteParts);
    }

    private async void PasteTargetEffectsImageButton_Click(object sender, RoutedEventArgs e)
    {
        await SafeRunAsync(() => PasteUserIntentImageAsync(UserIntentFieldNames.TargetEffects));
    }

    private async void UploadTargetEffectsImageButton_Click(object sender, RoutedEventArgs e)
    {
        await SafeRunAsync(() => UploadUserIntentImagesAsync(UserIntentFieldNames.TargetEffects));
    }

    private void OpenTargetEffectsImageDirectoryButton_Click(object sender, RoutedEventArgs e)
    {
        OpenUserIntentImageDirectory(UserIntentFieldNames.TargetEffects);
    }

    private void ViewUserIntentAssetsManifestButton_Click(object sender, RoutedEventArgs e)
    {
        var project = _project ?? throw new InvalidOperationException("请先新建或打开一个项目。");
        _userIntentAssetService.EnsureStructure(project);
        Process.Start(new ProcessStartInfo
        {
            FileName = project.UserIntentAssetManifestMarkdownPath,
            UseShellExecute = true
        });
    }

    private async void ImportGptOutputButton_Click(object sender, RoutedEventArgs e)
    {
        await SafeRunAsync(async () => await ImportGptOutputAsync(GptOutputBox.Text));
    }

    private async void BrowseGptOutputFileButton_Click(object sender, RoutedEventArgs e)
    {
        await SafeRunAsync(async () =>
        {
            using var dialog = new WinForms.OpenFileDialog
            {
                Title = "选择 GPT 分析 Markdown",
                Filter = "Markdown or text|*.md;*.txt|All files|*.*"
            };

            if (dialog.ShowDialog() != WinForms.DialogResult.OK)
            {
                return;
            }

            var text = await File.ReadAllTextAsync(dialog.FileName);
            GptOutputBox.Text = text;
            await ImportGptOutputAsync(text);
        });
    }

    private async void SupplementAssetsButton_Click(object sender, RoutedEventArgs e)
    {
        await SafeRunAsync(async () =>
        {
            using var dialog = new WinForms.FolderBrowserDialog
            {
                Description = "选择补充素材目录",
                SelectedPath = Directory.Exists(AssetSourceDirectoryBox.Text) ? AssetSourceDirectoryBox.Text : string.Empty,
                UseDescriptionForTitle = true
            };

            if (dialog.ShowDialog() != WinForms.DialogResult.OK)
            {
                return;
            }

            AssetSourceDirectoryBox.Text = dialog.SelectedPath;
            await ImportAssetsAsync();
        });
    }

    private void UseFallbackButton_Click(object sender, RoutedEventArgs e)
    {
        _useFallbackForCodex = true;
        AssetRequirementsText.Text += Environment.NewLine + "已选择：最终 Codex 任务使用降级方案。";
        UpdateButtonStates();
    }

    private async void GenerateFinalCodexPackageButton_Click(object sender, RoutedEventArgs e)
    {
        await SafeRunAsync(GenerateFinalCodexPackageAsync);
    }

    private void CopyChatGptPromptButton_Click(object sender, RoutedEventArgs e)
    {
        var prompt = _chatGptPackageResult?.PromptText ?? DefaultShortChatGptPrompt();
        System.Windows.Clipboard.SetText(prompt);
        _logger.Info("ChatGPT 提示词已复制到剪贴板。");
    }

    private void OpenPackageDirectoryButton_Click(object sender, RoutedEventArgs e)
    {
        if (_project is not null)
        {
            OpenDirectory(GetCurrentPackageDirectory(_project));
        }
    }

    private void OpenChatGptButton_Click(object sender, RoutedEventArgs e)
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = "https://chatgpt.com/",
            UseShellExecute = true
        });
    }

    private void CopyZipPathButton_Click(object sender, RoutedEventArgs e)
    {
        if (!string.IsNullOrWhiteSpace(_chatGptPackageResult?.ZipPath))
        {
            System.Windows.Clipboard.SetText(_chatGptPackageResult.ZipPath);
        _logger.Info("ChatGPT 上传包 ZIP 路径已复制到剪贴板。");
        }
    }

    private async void GenerateMarkdownButton_Click(object sender, RoutedEventArgs e)
    {
        await SafeRunAsync(GenerateMarkdownAsync);
    }

    private async void ScreenshotButton_Click(object sender, RoutedEventArgs e)
    {
        await SafeRunAsync(CaptureManualScreenshotAsync);
    }

    private async void CheckToolsButton_Click(object sender, RoutedEventArgs e)
    {
        await SafeRunAsync(CheckToolsAsync);
    }

    private async void DisableGlobalHotkeysButton_Click(object sender, RoutedEventArgs e)
    {
        await SafeRunAsync(DisableGlobalHotkeysAsync);
    }

    private async void Window_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            await SafeRunAsync(EmergencyStopAutomationAsync);
        }
    }

    private async void BrowseOutputButton_Click(object sender, RoutedEventArgs e)
    {
        using var dialog = new WinForms.FolderBrowserDialog
        {
            Description = "选择 WebRebuildRecorder 项目根目录",
            SelectedPath = Directory.Exists(OutputRootBox.Text) ? OutputRootBox.Text : _projectService.GetFallbackRootDirectory(),
            UseDescriptionForTitle = true
        };

        if (dialog.ShowDialog() == WinForms.DialogResult.OK)
        {
            OutputRootBox.Text = dialog.SelectedPath;
            UpdateProjectDirectoryPreview();
            await SafeRunAsync(SaveAppSettingsAsync);
        }
    }

    private void BrowseLocalCodePathButton_Click(object sender, RoutedEventArgs e)
    {
        using var dialog = new WinForms.FolderBrowserDialog
        {
            Description = "选择源码项目目录（可选）",
            SelectedPath = Directory.Exists(LocalCodePathBox.Text) ? LocalCodePathBox.Text : string.Empty,
            UseDescriptionForTitle = true
        };

        if (dialog.ShowDialog() == WinForms.DialogResult.OK)
        {
            LocalCodePathBox.Text = dialog.SelectedPath;
        }
    }

    private void BrowseAssetSourceDirectoryButton_Click(object sender, RoutedEventArgs e)
    {
        using var dialog = new WinForms.FolderBrowserDialog
        {
            Description = "选择用户素材目录（可选）",
            SelectedPath = Directory.Exists(AssetSourceDirectoryBox.Text) ? AssetSourceDirectoryBox.Text : string.Empty,
            UseDescriptionForTitle = true
        };

        if (dialog.ShowDialog() == WinForms.DialogResult.OK)
        {
            AssetSourceDirectoryBox.Text = dialog.SelectedPath;
        }
    }

    private void ProjectNameBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        UpdateProjectDirectoryPreview();
    }

    private void OutputRootBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        UpdateProjectDirectoryPreview();
    }

    private async void OutputRootBox_LostFocus(object sender, RoutedEventArgs e)
    {
        await SafeRunAsync(SaveAppSettingsAsync);
    }

    private async void SelectRegionButton_Click(object sender, RoutedEventArgs e)
    {
        await SafeRunAsync(async () =>
        {
            var area = _regionSelectionService.SelectRegion(this);
            if (area is null)
            {
                return;
            }

            _selectedArea = area;
            RegionRadio.IsChecked = true;
            SelectedRegionText.Text = $"区域：x={area.X}, y={area.Y}, {area.Width} x {area.Height}";
            if (_project is not null)
            {
                await SaveProfilesAsync();
            }

        _logger.Info($"已选择录屏区域：{_selectedArea}");
        });
    }

    private void IntervalPreset_Click(object sender, RoutedEventArgs e)
    {
        if (sender is System.Windows.Controls.Button { Tag: string interval })
        {
            IntervalBox.Text = interval;
        }
    }

    private void OpenProjectDirectoryButton_Click(object sender, RoutedEventArgs e)
    {
        if (_project is not null)
        {
            OpenDirectory(_project.ProjectDirectory);
        }
    }

    private void OpenMarkdownDirectoryButton_Click(object sender, RoutedEventArgs e)
    {
        if (_project is not null)
        {
            OpenDirectory(_project.MarkdownDirectory);
        }
    }

    private async Task LoadAppSettingsAsync()
    {
        _appSettings = await _appSettingsService.LoadAsync();
        _appSettings.Hotkeys ??= new HotkeySettings();
        _appSettings.FloatingToolbar ??= new FloatingToolbarSettings();

        var rootDirectory = string.IsNullOrWhiteSpace(_appSettings.ProjectsRootDirectory)
            ? _projectService.GetFallbackRootDirectory()
            : _appSettings.ProjectsRootDirectory;

        OutputRootBox.Text = rootDirectory;

        if (!string.IsNullOrWhiteSpace(_appSettings.LastProjectName))
        {
            ProjectNameBox.Text = _appSettings.LastProjectName;
        }

        SetComboByContent(BrowserCombo, _appSettings.LastBrowser);

        FullScreenRadio.IsChecked = true;
        SetSpeedComboByPreset("fast");
        SetObserveModeComboByMode(ObserveModes.HotspotPriority);
        SetRecordingStartModeCombo(RecordingStartMode.RecordBeforeOpenUrl);

        UpdateProjectDirectoryPreview();
        UpdateHotkeyText();
        _logger.Info($"App settings loaded: {_appSettingsService.SettingsPath}");

        if (IsFallbackDocumentsRoot(rootDirectory))
        {
            ProjectReadyText.Text = "当前默认项目目录位于 C 盘 Documents。录屏和抽帧文件可能较大，建议选择 E 盘或其他数据盘作为项目根目录。";
        }
    }

    private async Task SaveAppSettingsAsync()
    {
        _appSettings.Hotkeys ??= new HotkeySettings();
        _appSettings.ProjectsRootDirectory = OutputRootBox.Text.Trim();
        _appSettings.LastProjectName = ProjectNameBox.Text.Trim();
        _appSettings.LastBrowser = GetComboText(BrowserCombo);
        _appSettings.LastRecordingMode = RegionRadio.IsChecked == true ? RecordingAreaModes.Region : RecordingAreaModes.FullScreen;
        if (_floatingWindow is not null)
        {
            _appSettings.FloatingToolbar.Left = _floatingWindow.Left;
            _appSettings.FloatingToolbar.Top = Math.Max(0, _floatingWindow.Top - SystemParameters.WorkArea.Top);
            _appSettings.FloatingToolbar.Mode = _floatingWindow.ToolbarMode;
        }

        await _appSettingsService.SaveAsync(_appSettings);
        _logger.Info($"App settings saved: {_appSettingsService.SettingsPath}");
    }

    private async Task CreateProjectAsync()
    {
        var wizard = new NewProjectWizardWindow(_projectService.GetFallbackRootDirectory())
        {
            Owner = this
        };
        wizard.LoadInitialValues(
            _project is null ? ProjectNameBox.Text : string.Empty,
            _project is null ? UrlBox.Text : string.Empty,
            string.IsNullOrWhiteSpace(OutputRootBox.Text) ? _projectService.GetFallbackRootDirectory() : OutputRootBox.Text,
            _project is null ? LocalCodePathBox.Text : string.Empty,
            _project is null ? AssetSourceDirectoryBox.Text : string.Empty);

        if (wizard.ShowDialog() != true)
        {
            return;
        }

        ProjectNameBox.Text = wizard.ProjectNameValue;
        UrlBox.Text = NormalizeUrl(wizard.ReferenceUrlValue);
        OutputRootBox.Text = _projectService.ResolveProjectsRootDirectory(wizard.RootDirectoryValue);
        LocalCodePathBox.Text = wizard.LocalCodeProjectPathValue;
        AssetSourceDirectoryBox.Text = wizard.AssetSourceDirectoryValue;
        await SaveAppSettingsAsync();

        _project = await _projectService.CreateNewProjectAsync(new NewProjectOptions
        {
            ProjectName = wizard.ProjectNameValue,
            ReferenceUrl = UrlBox.Text,
            RootDirectory = OutputRootBox.Text
        });
        _project.LocalCodeProjectPath = wizard.LocalCodeProjectPathValue;
        _project.UserAssetSourceDirectory = wizard.AssetSourceDirectoryValue;
        _aiSiteWorkflowService.EnsureAiSiteProjectStructure(_project);
        _userIntentAssetService.EnsureStructure(_project);
        _actionLogger.AttachProject(_project);
        _actionLogger.SetSessionStart(DateTime.Now);
        _selectedArea = RecordingArea.FullScreen();
        FullScreenRadio.IsChecked = true;
        SelectedRegionText.Text = "区域：未选择";
        _recordingInfo = null;
        _frameResult = null;
        _frameOptions = null;
        _observationMarkdownPath = null;
        _framesZipPath = null;
        _observationPackagePath = null;
        _chatGptPackageResult = null;
        _websiteOpened = false;
        _manualScreenshotCounter = 0;

        await SaveProfilesAsync();
        if (Directory.Exists(_project.UserAssetSourceDirectory))
        {
            await _aiSiteWorkflowService.ImportAssetsAsync(_project, _project.UserAssetSourceDirectory);
        }

        await _projectService.SaveCurrentProjectAsync();
        ResetRuntimeStateForProject(_project);
        BindProjectToUi(_project);
        UpdateAssetManifestStatus(_project);
        UpdateUserIntentImageCounts(_project);
        await RefreshRecentProjectsAsync();

        ProjectDirectoryText.Text = _project.ProjectDirectory;
        ProjectReadyText.Text = "项目目录已创建，URL 已保存。";
        CurrentStatusText.Text = "当前状态：项目已创建";
        RecordingStateText.Text = "录屏：未开始";
        FramesCountText.Text = "截图数量：--";
        ChatGptPackageText.Text = "ChatGPT 上传包：尚未生成";
        ProjectNameBox.Text = _project.ProjectName;
        UrlBox.Text = _project.ReferenceUrl;
        SetWorkflowState(WorkflowState.ProjectCreated);
    }

    private async Task OpenWebsiteAsync()
    {
        var project = _project ?? throw new InvalidOperationException("请先新建项目。");
        var profile = BuildBrowserProfile();
        await SaveProfilesAsync();

        var url = NormalizeUrl(UrlBox.Text);
        UrlBox.Text = url;
        await _projectService.UpdateReferenceUrlAsync(project, url);

        var wasRecording = _recorder.IsRecording;
        _browserController = CreateBrowserController(profile);
        await _browserController.OpenAsync(url, profile);
        _openUrlTime = DateTime.Now;
        await _actionLogger.LogAsync(
            wasRecording ? "open-url-during-recording" : "open-url",
            url,
            wasRecording ? "录屏中打开目标网站。" : "打开网站预览。");

        _websiteOpened = true;
        if (wasRecording)
        {
            return;
        }

        SetWorkflowState(WorkflowState.WebsiteOpened);
        if (BuildAutoFlowOptions().ShowToolbarAfterOpenWebsite)
        {
            ShowFloatingWindow("待录屏");
            SetWorkflowState(WorkflowState.ToolbarReady);
        }
    }

    private async Task EnsureEmbeddedPreviewAsync()
    {
        if (_embeddedPreviewInitialized)
        {
            return;
        }

        EmbeddedPreviewStatusText.Text = "WebView2 预览：正在初始化...";
        try
        {
            await EmbeddedPreviewWebView.EnsureCoreWebView2Async();
            EmbeddedPreviewWebView.CoreWebView2.NavigationStarting += (_, args) =>
            {
                EmbeddedPreviewStatusText.Text = $"WebView2 预览：正在加载 {args.Uri}";
            };
            EmbeddedPreviewWebView.CoreWebView2.NavigationCompleted += (_, args) =>
            {
                EmbeddedPreviewStatusText.Text = args.IsSuccess
                    ? $"WebView2 预览：加载完成 {_lastEmbeddedPreviewUri}"
                    : $"WebView2 预览：加载失败，错误 {args.WebErrorStatus}";
            };
            _embeddedPreviewInitialized = true;
            EmbeddedPreviewStatusText.Text = "WebView2 预览：初始化完成。";
        }
        catch (WebView2RuntimeNotFoundException ex)
        {
            EmbeddedPreviewStatusText.Text = "WebView2 预览：未检测到 WebView2 Runtime。请安装 Microsoft Edge WebView2 Runtime。";
            _logger.Error("WebView2 Runtime not found.", ex);
            throw;
        }
    }

    private async Task PreviewReferenceInWebViewAsync()
    {
        var project = _project ?? throw new InvalidOperationException("请先新建或打开一个项目。");
        var rawUrl = string.IsNullOrWhiteSpace(UrlBox.Text)
            ? project.ReferenceUrl
            : UrlBox.Text;
        var url = NormalizeUrl(rawUrl);

        UrlBox.Text = url;
        await _projectService.UpdateReferenceUrlAsync(project, url);
        await NavigateEmbeddedPreviewAsync(url, "参考站");
    }

    private async Task PreviewOutputSiteInWebViewAsync()
    {
        var project = _project ?? throw new InvalidOperationException("请先新建或打开一个项目。");
        var indexPath = Path.Combine(project.ProjectDirectory, "output-site", "current", "index.html");

        if (!File.Exists(indexPath))
        {
            EmbeddedPreviewStatusText.Text = $"WebView2 预览：未找到 {indexPath}";
            _logger.Warn($"output-site/current/index.html not found: {indexPath}");
            return;
        }

        await NavigateEmbeddedPreviewAsync(new Uri(indexPath).AbsoluteUri, "output-site/current");
    }

    private async Task RefreshEmbeddedPreviewAsync()
    {
        if (string.IsNullOrWhiteSpace(_lastEmbeddedPreviewUri))
        {
            EmbeddedPreviewStatusText.Text = "WebView2 预览：没有可刷新的页面。";
            return;
        }

        await EnsureEmbeddedPreviewAsync();
        EmbeddedPreviewWebView.Reload();
        EmbeddedPreviewStatusText.Text = $"WebView2 预览：刷新 {_lastEmbeddedPreviewUri}";
    }

    private async Task NavigateEmbeddedPreviewAsync(string uri, string label)
    {
        await EnsureEmbeddedPreviewAsync();

        _lastEmbeddedPreviewUri = uri;
        EmbeddedPreviewStatusText.Text = $"WebView2 预览：打开 {label} - {uri}";
        _logger.Info($"Embedded WebView2 preview navigate: {label} {uri}");
        EmbeddedPreviewWebView.Source = new Uri(uri);
        UpdateButtonStates();
    }

    private async Task RunSourceSnapshotAsync()
    {
        var project = _project ?? throw new InvalidOperationException("请先新建或打开一个项目。");
        var rawUrl = string.IsNullOrWhiteSpace(UrlBox.Text)
            ? project.ReferenceUrl
            : UrlBox.Text;
        var url = NormalizeUrl(rawUrl);

        if (string.IsNullOrWhiteSpace(url))
        {
            throw new InvalidOperationException("请先填写参考站 URL。");
        }

        UrlBox.Text = url;
        await _projectService.UpdateReferenceUrlAsync(project, url);

        SourceSnapshotStatusText.Text = "Source Snapshot：正在渲染页面并提取结构化证据...";
        _logger.Info($"Source Snapshot started: {url}");

        var renderedEvidence = await CaptureRenderedSnapshotEvidenceAsync(url);

        SourceSnapshotStatusText.Text = "Source Snapshot：正在抓取 HTML 与生成资源清单...";
        var result = await _sourceSnapshotService.CaptureAsync(project, url, renderedEvidence);

        _lastSourceSnapshotResult = result;
        SourceSnapshotStatusText.Text =
            $"Source Snapshot：已生成 {result.Paths.Root}，资源 {CountResources(result.ResourceManifest)} 个，元素 {renderedEvidence.Elements.Count} 个，警告 {result.Analysis.Warnings.Count} 个。";
        _logger.Info($"Source Snapshot completed: {result.Paths.Root}");
        UpdateButtonStates();
    }

    private async Task<SourceSnapshotRenderedEvidence> CaptureRenderedSnapshotEvidenceAsync(string url)
    {
        try
        {
            await NavigateEmbeddedPreviewAndWaitAsync(url, TimeSpan.FromSeconds(35));
            var scriptResult = await EmbeddedPreviewWebView.CoreWebView2.ExecuteScriptAsync(
                SourceSnapshotRenderedEvidenceScript);
            var evidence = JsonSerializer.Deserialize<SourceSnapshotRenderedEvidence>(
                scriptResult,
                WrbJsonOptions.Default);

            if (evidence is null)
            {
                return new SourceSnapshotRenderedEvidence
                {
                    RenderSucceeded = false,
                    RenderError = "WebView2 returned empty rendered evidence."
                };
            }

            evidence.RenderSucceeded = true;
            return evidence;
        }
        catch (Exception ex)
        {
            _logger.Error("Source Snapshot rendered evidence capture failed.", ex);
            return new SourceSnapshotRenderedEvidence
            {
                RenderSucceeded = false,
                RenderError = ex.Message
            };
        }
    }

    private async Task NavigateEmbeddedPreviewAndWaitAsync(string url, TimeSpan timeout)
    {
        await EnsureEmbeddedPreviewAsync();
        var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        void Handler(object? sender, CoreWebView2NavigationCompletedEventArgs args)
        {
            if (args.IsSuccess)
            {
                tcs.TrySetResult(true);
            }
            else
            {
                tcs.TrySetException(
                    new InvalidOperationException($"WebView2 navigation failed: {args.WebErrorStatus}"));
            }
        }

        EmbeddedPreviewWebView.CoreWebView2.NavigationCompleted += Handler;
        try
        {
            _lastEmbeddedPreviewUri = url;
            EmbeddedPreviewStatusText.Text = $"WebView2 预览：Source Snapshot 正在加载 {url}";
            EmbeddedPreviewWebView.Source = new Uri(url);

            var completed = await Task.WhenAny(tcs.Task, Task.Delay(timeout));
            if (completed != tcs.Task)
            {
                throw new TimeoutException(
                    $"WebView2 navigation timed out after {timeout.TotalSeconds:n0} seconds.");
            }

            await tcs.Task;
        }
        finally
        {
            EmbeddedPreviewWebView.CoreWebView2.NavigationCompleted -= Handler;
        }
    }

    private static int CountResources(SourceSnapshotResourceManifest manifest)
    {
        return manifest.Css.Count
            + manifest.JavaScript.Count
            + manifest.Images.Count
            + manifest.Fonts.Count
            + manifest.Videos.Count
            + manifest.Other.Count;
    }

    private async Task OpenDetachedPreviewWindowAsync()
    {
        var uri = await ResolvePreviewUriForDetachedWindowAsync();

        if (_detachedPreviewWindow is null)
        {
            _detachedPreviewWindow = new DetachedPreviewWindow(_logger)
            {
                Owner = this
            };
            _detachedPreviewWindow.Closed += (_, _) =>
            {
                _detachedPreviewWindow = null;
                UpdateButtonStates();
            };
        }

        if (!_detachedPreviewWindow.IsVisible)
        {
            _detachedPreviewWindow.Show();
        }

        if (_detachedPreviewWindow.WindowState == WindowState.Minimized)
        {
            _detachedPreviewWindow.WindowState = WindowState.Normal;
        }

        _detachedPreviewWindow.Activate();
        await _detachedPreviewWindow.NavigateAsync(uri);

        _lastEmbeddedPreviewUri = uri;
        EmbeddedPreviewStatusText.Text = $"WebView2 预览：已弹出独立窗口 - {uri}";
        UpdateButtonStates();
    }

    private async Task<string> ResolvePreviewUriForDetachedWindowAsync()
    {
        if (!string.IsNullOrWhiteSpace(_lastEmbeddedPreviewUri))
        {
            return _lastEmbeddedPreviewUri;
        }

        var project = _project ?? throw new InvalidOperationException("请先新建或打开一个项目。");
        var rawUrl = string.IsNullOrWhiteSpace(UrlBox.Text)
            ? project.ReferenceUrl
            : UrlBox.Text;
        var url = NormalizeUrl(rawUrl);

        if (string.IsNullOrWhiteSpace(url))
        {
            throw new InvalidOperationException("请先填写参考站 URL，或先在内嵌预览中打开一个页面。");
        }

        UrlBox.Text = url;
        await _projectService.UpdateReferenceUrlAsync(project, url);
        return url;
    }

    private void CloseDetachedPreviewWindow()
    {
        if (_detachedPreviewWindow is null)
        {
            return;
        }

        var window = _detachedPreviewWindow;
        _detachedPreviewWindow = null;
        window.Close();
    }

    private async Task StartRecordingAsync()
    {
        await StartManualRecordingFlowAsync();
    }

    private async Task StartManualRecordingFlowAsync()
    {
        _ = _project ?? throw new InvalidOperationException("请先新建或打开一个项目。");
        var startMode = GetSelectedRecordingStartMode();
        if (startMode == RecordingStartMode.RecordBeforeOpenUrl
            && !await ConfirmRecordBeforeOpenUrlNoticeAsync("用户取消人工录屏。"))
        {
            return;
        }

        if (BuildAutoFlowOptions().ShowCountdownBeforeRecording)
        {
            await ShowCountdownAsync("开始");
        }

        if (startMode == RecordingStartMode.OpenUrlBeforeRecording)
        {
            if (!_websiteOpened)
            {
                await OpenWebsiteAsync();
            }

            await StartRecordingCoreAsync(isAutoObserve: false, startMode, startedBeforeOpenUrl: false);
            return;
        }

        await StartRecordingCoreAsync(isAutoObserve: false, startMode, startedBeforeOpenUrl: startMode == RecordingStartMode.RecordBeforeOpenUrl);
        if (startMode == RecordingStartMode.RecordBeforeOpenUrl)
        {
            await _actionLogger.LogAsync("recording-start-before-open-url", "", "先启动录屏，再打开网站，以捕捉加载动画。");
            await Task.Delay(500);
            await OpenWebsiteAsync();
            SetWorkflowState(WorkflowState.Recording);
        }
    }

    private async Task StopRecordingAsync()
    {
        await StopRecordingCoreAsync();
        if (BuildAutoFlowOptions().AutoExtractAfterStop && !_automaticPipelineRunning)
        {
            await RunPostRecordingPipelineAsync();
        }
    }

    private async Task StartAutomaticClosedLoopAsync()
    {
        if (_automaticPipelineRunning)
        {
            return;
        }

        _automaticPipelineRunning = true;
        try
        {
            var options = BuildAutoFlowOptions();
            var project = _project ?? throw new InvalidOperationException("请先新建或打开一个项目。");
            await SaveProfilesAsync();
            var startMode = GetSelectedRecordingStartMode();
            if (startMode == RecordingStartMode.RecordBeforeOpenUrl
                && !await ConfirmRecordBeforeOpenUrlNoticeAsync("用户取消自动观察录屏。"))
            {
                return;
            }

            if (options.ShowCountdownBeforeRecording)
            {
                await ShowCountdownAsync("开始");
            }

            if (startMode == RecordingStartMode.OpenUrlBeforeRecording)
            {
                if (!_websiteOpened)
                {
                    await OpenWebsiteAsync();
                }

                await StartRecordingCoreAsync(isAutoObserve: true, startMode, startedBeforeOpenUrl: false);
            }
            else
            {
                await StartRecordingCoreAsync(isAutoObserve: true, startMode, startedBeforeOpenUrl: startMode == RecordingStartMode.RecordBeforeOpenUrl);
                if (startMode == RecordingStartMode.RecordBeforeOpenUrl)
                {
                    await _actionLogger.LogAsync("recording-start-before-open-url", "", "先启动录屏，再打开网站，以捕捉加载动画。");
                    await Task.Delay(500);
                    await OpenWebsiteAsync();
                }
            }

            var actionProfile = await LoadActionProfileForCurrentUiAsync();
            _activeActionProfile = actionProfile;
            _floatingWindow?.SetSpeedPreset(ToSpeedPresetDisplay(actionProfile.SpeedPreset));
            await CollectInteractionTargetsForCurrentPageAsync(project, actionProfile);

            if (string.Equals(actionProfile.ObserveMode, ObserveModes.ManualLed, StringComparison.OrdinalIgnoreCase))
            {
                _hadManualTakeover = true;
                SetWorkflowState(WorkflowState.ManualControl);
                _floatingWindow?.SetState("人工主导中：请手动操作网页，完成后点击停止");
                await _actionLogger.LogAsync("manual-led-start", "", "人工主导模式：自动操作不移动鼠标，不自动停止录屏；等待用户手动停止。", new
                {
                    speedPreset = actionProfile.SpeedPreset,
                    observeMode = actionProfile.ObserveMode
                });
                return;
            }

            if (options.AutoObservePage)
            {
                SetWorkflowState(WorkflowState.AutoObserving);
                _manualMouseDetectionTimer.Start();
                _autoObserveCancellation = new CancellationTokenSource();
                _autoObserveTask = RunAutoObserveAsync(actionProfile, _autoObserveCancellation.Token);
                await _autoObserveTask;
                _manualMouseDetectionTimer.Stop();
            }

            if (_recorder.IsRecording)
            {
                await StopRecordingCoreAsync();
            }

            if (options.AutoExtractAfterStop)
            {
                await RunPostRecordingPipelineAsync();
            }

            SetWorkflowState(WorkflowState.Completed);
            if (options.ShowCompletionPrompt)
            {
                ShowCompletionPrompt();
            }
        }
        finally
        {
            _automaticPipelineRunning = false;
            _manualMouseDetectionTimer.Stop();
        }
    }

    private async Task<bool> ConfirmRecordBeforeOpenUrlNoticeAsync(string cancelDescription)
    {
        var result = System.Windows.MessageBox.Show(
            "为了完整捕捉网站加载动效、首屏进入动画、图片渐显和文字入场效果，程序将先启动录屏，再打开浏览器访问目标网站。\n\n点击“确定”后将开始倒计时，然后自动启动录屏并打开网站。",
            "录屏启动说明",
            MessageBoxButton.OKCancel,
            MessageBoxImage.Information);

        if (result == MessageBoxResult.OK)
        {
            await _actionLogger.LogAsync(
                "record-before-open-url-notice-confirmed",
                "",
                "用户确认先录屏再打开网站，以捕捉加载和首屏动画。");
            return true;
        }

        await _actionLogger.LogAsync(
            "record-before-open-url-notice-cancelled",
            "",
            cancelDescription);
        return false;
    }

    private async Task StartRecordingCoreAsync(
        bool isAutoObserve,
        RecordingStartMode? startModeOverride = null,
        bool startedBeforeOpenUrl = false)
    {
        if (_recorder.IsRecording)
        {
            return;
        }

        var project = _project ?? throw new InvalidOperationException("请先新建项目。");
        await _projectService.PrepareNextRecordingAsync(project);
        await SaveProfilesAsync();

        var area = BuildRecordingArea();
        var options = new RecordingOptions
        {
            FfmpegPath = FfmpegPathBox.Text.Trim(),
            OutputPath = project.RecordingFilePath,
            FrameRate = ParseInt(RecordingFpsBox.Text, 1, 120, "帧率"),
            Crf = ParseInt(RecordingCrfBox.Text, 0, 51, "CRF"),
            Area = area
        };

        _recordingStart = DateTime.Now;
        _recordingEnd = DateTime.MinValue;
        if (startedBeforeOpenUrl)
        {
            _openUrlTime = null;
        }
        _hadManualTakeover = false;
        _manualMouseAnomalyCount = 0;
        _currentRecordingIsAutoObserve = isAutoObserve;
        _currentRecordingStartMode = startModeOverride ?? GetSelectedRecordingStartMode();
        _currentRecordingStartedBeforeOpenUrl = startedBeforeOpenUrl;
        _recordingInfo = null;
        _frameResult = null;
        _frameOptions = null;
        _observationMarkdownPath = null;
        _framesZipPath = null;
        _observationPackagePath = null;
        _chatGptPackageResult = null;
        _codexPackageResult = null;
        _assetRequirementReport = null;
        _lastSourceSnapshotResult = null;
        _currentRunId = null;
        _useFallbackForCodex = false;
        project.LastRunId = string.Empty;
        _actionLogger.SetRecordingStart(_recordingStart);

        await _recorder.StartAsync(options);
        await _actionLogger.LogAsync("recording-start", project.RecordingFilePath, isAutoObserve ? "开始自动录屏" : "开始录屏");
        ShowFloatingWindow(isAutoObserve ? "自动观察中" : "人工录制");

        _recordingTimer.Start();
        SetWorkflowState(WorkflowState.Recording);
        RecordingStateText.Text = "录屏：进行中";
    }

    private async Task StopRecordingCoreAsync()
    {
        if (!_recorder.IsRecording)
        {
            return;
        }

        var project = _project ?? throw new InvalidOperationException("当前没有项目。");
        await StopAutomationAsync();
        await _actionLogger.LogAsync("recording-stop", project.RecordingFilePath, "停止录屏");

        await _recorder.StopAsync();
        _recordingEnd = DateTime.Now;
        _actionLogger.SetRecordingEnd(_recordingEnd);
        _recordingTimer.Stop();
        _manualMouseDetectionTimer.Stop();

        _recordingInfo = await ProbeRecordingInfoWithFallbackAsync(project, BuildRecordingArea());
        ApplyRecordingMetadata(_recordingInfo);
        await _projectService.WriteRecordingInfoAsync(project, _recordingInfo);
        UpdateRecordingInfoUi(_recordingInfo);
        UpdateFrameEstimateText();

        SetWorkflowState(WorkflowState.RecordingStopped);
        RecordingStateText.Text = "录屏：已停止";
        _floatingWindow?.SetState("录屏已停止");
    }

    private async Task RunPostRecordingPipelineAsync()
    {
        var options = BuildAutoFlowOptions();
        await ExtractFramesAsync();

        if (options.AutoGenerateMarkdownAfterPackage)
        {
            await GenerateMarkdownAsync();
        }

        if (options.AutoPackageAfterExtract)
        {
            SetWorkflowState(WorkflowState.Packaging);
            if (string.IsNullOrWhiteSpace(_observationMarkdownPath) || !File.Exists(_observationMarkdownPath))
            {
                await GenerateMarkdownAsync();
            }

            _framesZipPath = await _zipPackageService.CreateFramesZipAsync(_project!, _frameResult!);
            await _actionLogger.LogAsync("package", Path.GetFileName(_framesZipPath), "打包截图");
            _observationPackagePath = await _zipPackageService.CreateObservationPackageAsync(_project!, _frameResult, _observationMarkdownPath);
            await _actionLogger.LogAsync("package", Path.GetFileName(_observationPackagePath), "打包观察资料");
            try
            {
                await CreateAiGptAnalysisPackageAsync(_project!);
                await _actionLogger.LogAsync("package-chatgpt", Path.GetFileName(_chatGptPackageResult!.ZipPath), $"GPT 分析包已生成，包含 {_chatGptPackageResult.SelectedFrameCount} 张参考帧。");
                UpdateChatGptPackageUi();
                _floatingWindow?.SetCompleted(TimeUtil.FormatFileSize(_chatGptPackageResult.ZipSizeBytes));
            }
            catch (Exception ex)
            {
                _logger.Error("GPT 分析包生成失败。", ex);
                ChatGptPackageText.Text = "GPT 分析包生成失败，请查看日志。";
            }
        }
    }

    private async Task ExtractFramesAsync()
    {
        var project = _project ?? throw new InvalidOperationException("请先新建项目并完成录屏。");
        if (_recorder.IsRecording)
        {
            throw new InvalidOperationException("视频仍在录制，不能开始抽帧。");
        }

        if (!File.Exists(project.RecordingFilePath))
        {
            throw new FileNotFoundException("录屏文件不存在。", project.RecordingFilePath);
        }

        SetWorkflowState(WorkflowState.ExtractingFrames);
        var interval = ParseInt(IntervalBox.Text, 1, 1000, "抽帧间隔");
        var duration = _recordingInfo?.DurationSeconds ?? await _ffprobeService.GetDurationSecondsAsync(FfprobePathBox.Text.Trim(), project.RecordingFilePath);
        var sourceFps = _recordingInfo?.FrameRate > 0 ? _recordingInfo.FrameRate : 30d;
        var requestedFps = 1000d / interval;
        var effectiveFps = AllowDuplicateFramesCheckBox.IsChecked == true ? requestedFps : Math.Min(requestedFps, sourceFps);
        var estimatedFrames = duration * effectiveFps;
        _estimatedFrameCount = Math.Max(1, (int)Math.Ceiling(estimatedFrames));

        ExtractEstimateText.Text = $"视频时长 {TimeUtil.FormatTime(TimeSpan.FromSeconds(duration))}，间隔 {interval} ms，预计 {Math.Ceiling(estimatedFrames):0} 张，输出到 frames/{project.Slug}_{project.RecordingId}_{interval}ms/";

        if (estimatedFrames > 1000)
        {
            var warning = "当前视频较长且抽帧间隔较小，将生成大量截图。可能占用较多硬盘空间，也会增加后续 ChatGPT 分析难度。";
            var result = System.Windows.MessageBox.Show(warning, estimatedFrames > 3000 ? "强提醒：截图数量很大" : "确认抽帧", MessageBoxButton.OKCancel, MessageBoxImage.Warning);
            if (result != MessageBoxResult.OK)
            {
                return;
            }
        }

        var outputDirectory = Path.Combine(project.FramesDirectory, $"{project.Slug}_{project.RecordingId}_{interval}ms");
        CleanFrameOutputDirectory(outputDirectory);
        _activeExtractionDirectory = outputDirectory;

        _frameOptions = new FrameExtractOptions
        {
            FfmpegPath = FfmpegPathBox.Text.Trim(),
            VideoPath = project.RecordingFilePath,
            OutputDirectory = outputDirectory,
            ProjectDirectory = project.ProjectDirectory,
            Prefix = "frame",
            IntervalMs = interval,
            Format = GetComboText(FormatCombo),
            JpegQuality = ParseInt(QualityBox.Text, 1, 100, "图片质量"),
            UseOriginalSize = OriginalSizeRadio.IsChecked == true,
            CustomWidth = ParseInt(CustomWidthBox.Text, 1, 20000, "自定义宽度"),
            CustomHeight = ParseInt(CustomHeightBox.Text, 1, 20000, "自定义高度"),
            KeepAspectRatio = KeepAspectCheckBox.IsChecked == true,
            MaxFrames = TryParseNullableInt(MaxFramesBox.Text),
            SourceFrameRate = sourceFps,
            AllowDuplicateFramesAboveSourceFps = AllowDuplicateFramesCheckBox.IsChecked == true
        };

        _frameProgressTimer.Start();
        try
        {
            _frameResult = await _frameExtractor.ExtractAsync(_frameOptions);
        }
        finally
        {
            _frameProgressTimer.Stop();
        }

        if (_frameResult.TotalFrames == 0)
        {
            throw new InvalidOperationException("抽帧完成但输出 0 张图片，请检查视频文件和 FFmpeg 参数。");
        }

        FramesCountText.Text = $"截图数量：{_frameResult.TotalFrames}";
        ObservationText.Text = $"抽帧完成：{_frameResult.TotalFrames} 张。可以生成 observation.md。";
        _floatingWindow?.SetExtractionCompleted(_frameResult.TotalFrames);
        await _actionLogger.LogAsync("extract-frames", $"{interval}ms", $"生成 {_frameResult.TotalFrames} 张截图");
    }

    private async Task PackageAsync()
    {
        var project = _project ?? throw new InvalidOperationException("请先新建项目。");
        if (_frameResult is null)
        {
            throw new InvalidOperationException("请先抽帧。");
        }

        SetWorkflowState(WorkflowState.Packaging);
        _framesZipPath = await _zipPackageService.CreateFramesZipAsync(project, _frameResult);
        await _actionLogger.LogAsync("package", Path.GetFileName(_framesZipPath), "打包截图");

        if (string.IsNullOrWhiteSpace(_observationMarkdownPath) || !File.Exists(_observationMarkdownPath))
        {
            await GenerateMarkdownAsync();
        }

        _observationPackagePath = await _zipPackageService.CreateObservationPackageAsync(project, _frameResult, _observationMarkdownPath);
        await _actionLogger.LogAsync("package", Path.GetFileName(_observationPackagePath), "打包观察资料");
        await CreateAiGptAnalysisPackageAsync(project);
        await _actionLogger.LogAsync("package-chatgpt", Path.GetFileName(_chatGptPackageResult!.ZipPath), $"GPT 分析包已生成，包含 {_chatGptPackageResult.SelectedFrameCount} 张参考帧。");
        UpdateChatGptPackageUi();
        _floatingWindow?.SetCompleted(TimeUtil.FormatFileSize(_chatGptPackageResult.ZipSizeBytes));
        ObservationText.Text = $"打包完成：{Path.GetFileName(_framesZipPath)}";
    }

    private async Task GenerateChatGptPackageAsync()
    {
        var project = _project ?? throw new InvalidOperationException("请先新建项目。");
        if (_frameResult is null)
        {
            throw new InvalidOperationException("请先抽帧。");
        }

        if (string.IsNullOrWhiteSpace(_observationMarkdownPath) || !File.Exists(_observationMarkdownPath))
        {
            await GenerateMarkdownAsync();
        }

        await CreateAiGptAnalysisPackageAsync(project);
        await _actionLogger.LogAsync("package-chatgpt", Path.GetFileName(_chatGptPackageResult!.ZipPath), $"GPT 分析包已生成，包含 {_chatGptPackageResult.SelectedFrameCount} 张参考帧。");
        UpdateChatGptPackageUi();
        _floatingWindow?.SetCompleted(TimeUtil.FormatFileSize(_chatGptPackageResult.ZipSizeBytes));

        if (_chatGptPackageResult.ZipSizeBytes > 100L * 1024 * 1024)
        {
            _logger.Warn("ChatGPT 上传包超过 100MB，建议减少 selected-frames 数量或提高抽帧间隔。");
        }
        else if (_chatGptPackageResult.ZipSizeBytes > 50L * 1024 * 1024)
        {
            _logger.Warn("ChatGPT 上传包超过 50MB，建议关注上传速度和处理耗时。");
        }
    }

    private async Task ImportAssetsAsync()
    {
        var project = _project ?? throw new InvalidOperationException("请先新建或打开一个项目。");
        SyncProjectSettingsFromUi(project);
        if (string.IsNullOrWhiteSpace(project.UserAssetSourceDirectory))
        {
            throw new InvalidOperationException("请先选择用户素材目录。");
        }

        var manifest = await _aiSiteWorkflowService.ImportAssetsAsync(project, project.UserAssetSourceDirectory);
        await _projectService.SaveCurrentProjectAsync();
        UpdateAssetManifestStatus(project, manifest);
    }

    private async Task CreateAiGptAnalysisPackageAsync(RebuildProject project)
    {
        if (_frameResult is null)
        {
            throw new InvalidOperationException("请先完成抽帧，再生成 GPT 分析包。");
        }

        var intent = BuildUserIntentFromUi();
        var userProvidedIntent = intent.IsValid;
        if (!intent.IsValid)
        {
            intent = UserIntent.CreateEmptyFallback();
            UserIntentStatusText.Text = "未填写主观意图，已使用默认说明生成 GPT 分析包。";
        }

        SyncProjectSettingsFromUi(project);
        var runId = await EnsureCurrentRunIdAsync(project);
        var result = await _aiSiteWorkflowService.CreateGptAnalysisPackageAsync(
            project,
            runId,
            intent,
            _frameResult,
            _observationMarkdownPath,
            _recordingInfo?.VideoPath ?? project.RecordingFilePath);

        _chatGptPackageResult = new ChatGptPackageResult
        {
            ZipPath = result.ZipPath,
            ZipSizeBytes = result.ZipSizeBytes,
            SelectedFrameCount = result.ReferenceFrameCount,
            PromptText = await File.ReadAllTextAsync(result.PromptPath)
        };
        UserIntentStatusText.Text = userProvidedIntent
            ? "已保存主观意图，并写入 GPT 分析包。"
            : "未填写主观意图，已使用默认说明生成 GPT 分析包。";
        ChatGptPackageText.Text = $"GPT 分析包已生成：{result.ZipPath}";
        await _projectService.SaveCurrentProjectAsync();
    }

    private async Task ImportGptOutputAsync(string analysisText)
    {
        var project = _project ?? throw new InvalidOperationException("请先新建或打开一个项目。");
        var runId = await EnsureCurrentRunIdAsync(project);
        _assetRequirementReport = await _aiSiteWorkflowService.ImportGptAnalysisAsync(project, runId, analysisText);
        _useFallbackForCodex = false;
        UpdateAssetRequirementsUi();
        await _projectService.SaveCurrentProjectAsync();
    }

    private async Task GenerateFinalCodexPackageAsync()
    {
        var project = _project ?? throw new InvalidOperationException("请先新建或打开一个项目。");
        var runId = await EnsureCurrentRunIdAsync(project);
        _codexPackageResult = await _aiSiteWorkflowService.CreateFinalCodexPackageAsync(project, runId, _useFallbackForCodex);
        FinalCodexPackageText.Text = "最终 Codex 包已生成：" + Environment.NewLine
            + _codexPackageResult.ZipPath + Environment.NewLine
            + $"大小：{TimeUtil.FormatFileSize(_codexPackageResult.ZipSizeBytes)}";
        await _projectService.SaveCurrentProjectAsync();
    }

    private async Task<string> EnsureCurrentRunIdAsync(RebuildProject project)
    {
        _aiSiteWorkflowService.EnsureAiSiteProjectStructure(project);
        if (string.IsNullOrWhiteSpace(_currentRunId)
            || !Directory.Exists(Path.Combine(project.RunsDirectory, _currentRunId)))
        {
            _currentRunId = _aiSiteWorkflowService.GetOrCreateRunId(project);
        }

        project.LastRunId = _currentRunId;
        await _projectService.SaveCurrentProjectAsync();
        return _currentRunId;
    }

    private UserIntent BuildUserIntentFromUi()
    {
        return new UserIntent
        {
            FirstImpression = FirstImpressionBox.Text.Trim(),
            FavoriteParts = FavoritePartsBox.Text.Trim(),
            TargetEffects = TargetEffectsBox.Text.Trim(),
            AvoidParts = AvoidPartsBox.Text.Trim(),
            DesiredOutcome = DesiredOutcomeBox.Text.Trim()
        };
    }

    private async Task LoadRunUiStateAsync(RebuildProject project)
    {
        if (string.IsNullOrWhiteSpace(project.LastRunId))
        {
            return;
        }

        _currentRunId = project.LastRunId;
        var intent = await _aiSiteWorkflowService.LoadUserIntentAsync(project, project.LastRunId);
        if (intent is not null)
        {
            FirstImpressionBox.Text = intent.FirstImpression;
            FavoritePartsBox.Text = intent.FavoriteParts;
            TargetEffectsBox.Text = intent.TargetEffects;
            AvoidPartsBox.Text = intent.AvoidParts;
            DesiredOutcomeBox.Text = intent.DesiredOutcome;
        UserIntentStatusText.Text = $"已从 run {project.LastRunId} 载入用户主观意图。";
        }

        _assetRequirementReport = await _aiSiteWorkflowService.TryLoadAssetRequirementReportAsync(project, project.LastRunId);
        UpdateAssetRequirementsUi();
    }

    private void UpdateAssetManifestStatus(RebuildProject project, AssetManifest? manifest = null)
    {
        manifest ??= _aiSiteWorkflowService.LoadAssetManifest(project);
        var imageCount = manifest.Assets.Count(asset => asset.Type is "image" or "logo");
        AssetManifestStatusText.Text = "素材清单：" + manifest.Assets.Count + " 个文件，"
            + imageCount + " 个图片/Logo 素材。路径：" + project.AssetManifestPath;
    }

    private Task PasteUserIntentImageAsync(string field)
    {
        var project = _project ?? throw new InvalidOperationException("请先新建或打开一个项目。");
        if (!System.Windows.Clipboard.ContainsImage())
        {
            System.Windows.MessageBox.Show(
                "剪贴板中没有图片。请先截图或复制图片后再粘贴。",
                "粘贴图片",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return Task.CompletedTask;
        }

        var image = System.Windows.Clipboard.GetImage();
        if (image is null)
        {
            System.Windows.MessageBox.Show(
                "剪贴板中没有可保存的图片。请重新截图或复制图片后再试。",
                "粘贴图片",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return Task.CompletedTask;
        }

        _userIntentAssetService.SaveClipboardImage(project, field, image);
        UpdateUserIntentImageCounts(project);
        UserIntentStatusText.Text = $"已添加{UserIntentFieldNames.GetDisplayName(field)}图片。";
        return Task.CompletedTask;
    }

    private Task UploadUserIntentImagesAsync(string field)
    {
        var project = _project ?? throw new InvalidOperationException("请先新建或打开一个项目。");
        using var dialog = new WinForms.OpenFileDialog
        {
            Title = $"选择{UserIntentFieldNames.GetDisplayName(field)}参考图片",
            Filter = "图片文件 (*.png;*.jpg;*.jpeg;*.webp)|*.png;*.jpg;*.jpeg;*.webp",
            Multiselect = true
        };

        if (dialog.ShowDialog() != WinForms.DialogResult.OK)
        {
            return Task.CompletedTask;
        }

        foreach (var fileName in dialog.FileNames)
        {
            _userIntentAssetService.ImportImage(project, field, fileName);
        }

        UpdateUserIntentImageCounts(project);
        UserIntentStatusText.Text = $"已导入 {dialog.FileNames.Length} 张{UserIntentFieldNames.GetDisplayName(field)}图片。";
        return Task.CompletedTask;
    }

    private void OpenUserIntentImageDirectory(string field)
    {
        var project = _project ?? throw new InvalidOperationException("请先新建或打开一个项目。");
        _userIntentAssetService.EnsureStructure(project);
        OpenDirectory(_userIntentAssetService.GetFieldDirectory(project, field));
    }

    private void UpdateUserIntentImageCounts(RebuildProject? project)
    {
        if (FavoriteImagesCountText is null || TargetEffectsImagesCountText is null)
        {
            return;
        }

        if (project is null)
        {
            FavoriteImagesCountText.Text = "已添加 0 张图片";
            TargetEffectsImagesCountText.Text = "已添加 0 张图片";
            return;
        }

        _userIntentAssetService.EnsureStructure(project);
        FavoriteImagesCountText.Text = $"已添加 {_userIntentAssetService.CountByField(project, UserIntentFieldNames.FavoriteParts)} 张图片";
        TargetEffectsImagesCountText.Text = $"已添加 {_userIntentAssetService.CountByField(project, UserIntentFieldNames.TargetEffects)} 张图片";
    }

    private void UpdateAssetRequirementsUi()
    {
        if (_assetRequirementReport is null)
        {
            AssetRequirementsText.Text = "素材需求：尚未导入 GPT 结果";
            return;
        }

        var state = _assetRequirementReport.BlockingLevel switch
        {
            "blocking" => "当前素材不足，不建议直接生成最终指令",
            "warning" => "建议补充素材，也可降级继续",
            _ => "仅靠代码可实现或无素材问题"
        };
        var builder = new System.Text.StringBuilder();
        builder.AppendLine($"素材需求：{state}，共 {_assetRequirementReport.Items.Count} 项。");
        builder.AppendLine($"阻塞级别：{_assetRequirementReport.BlockingLevel}");
        foreach (var item in _assetRequirementReport.Items.Take(5))
        {
            builder.AppendLine($"- {item.Title}");
            if (!string.IsNullOrWhiteSpace(item.Effect))
            {
                builder.AppendLine($"  影响动效：{item.Effect}");
            }
            if (item.RequiredAssets.Count > 0)
            {
                builder.AppendLine($"  缺失/必需素材：{string.Join(", ", item.RequiredAssets)}");
            }
            if (!string.IsNullOrWhiteSpace(item.Recommendation))
            {
                builder.AppendLine($"  建议：{item.Recommendation}");
            }
        }

        AssetRequirementsText.Text = builder.ToString();
    }

    private async Task GenerateMarkdownAsync()
    {
        var project = _project ?? throw new InvalidOperationException("请先新建项目。");
        _recordingInfo ??= await ProbeRecordingInfoWithFallbackAsync(project, BuildRecordingArea());
        ApplyRecordingMetadata(_recordingInfo);

        var interval = _frameResult?.IntervalMs ?? ParseInt(IntervalBox.Text, 1, 1000, "抽帧间隔");
        var mdOptions = await _projectService.LoadMdGenerationOptionsAsync(project);
        _observationMarkdownPath = Path.Combine(project.MarkdownDirectory, $"{project.Slug}_{project.RecordingId}_{interval}ms_observation.md");

        SetWorkflowState(WorkflowState.GeneratingMarkdown);
        await _markdownGenerator.GenerateAsync(new ObservationContext
        {
            Project = project,
            RecordingInfo = _recordingInfo,
            FrameResult = _frameResult,
            FrameOptions = _frameOptions,
            ActionProfile = _activeActionProfile,
            MarkdownOptions = mdOptions,
            OutputPath = _observationMarkdownPath
        });

        await _actionLogger.LogAsync("generate-md", Path.GetFileName(_observationMarkdownPath), "生成 observation.md");
        ObservationText.Text = $"已生成：{_observationMarkdownPath}";
    }

    private async Task CaptureManualScreenshotAsync()
    {
        var project = await EnsureProjectAsync();
        _manualScreenshotCounter++;

        var elapsed = _recorder.IsRecording ? DateTime.Now - _recordingStart : TimeSpan.Zero;
        var format = GetComboText(FormatCombo);
        var extension = string.Equals(format, "PNG", StringComparison.OrdinalIgnoreCase) ? "png" : "jpg";
        var manualDirectory = Path.Combine(project.FramesDirectory, "manual");
        var fileName = $"manual_{_manualScreenshotCounter:0000}_{TimeUtil.FormatTimeCompact(elapsed)}.{extension}";
        var outputPath = Path.Combine(manualDirectory, fileName);

        _screenCaptureService.Capture(BuildRecordingArea(), outputPath, format, ParseInt(QualityBox.Text, 1, 100, "图片质量"));
        var url = await GetCurrentUrlOrFallbackAsync();
        await _markerService.AddMarkerAsync(project, elapsed, "manual-screenshot", url, $"手动截图：{fileName}");
        await _actionLogger.LogAsync("manual-screenshot", fileName, "截取当前画面");
    }

    private async Task AddManualMarkerAsync()
    {
        var project = await EnsureProjectAsync();
        var elapsed = _recorder.IsRecording ? DateTime.Now - _recordingStart : TimeSpan.Zero;
        var url = await GetCurrentUrlOrFallbackAsync();
        var markerType = _floatingWindow?.MarkerType ?? "other";
        await _markerService.AddMarkerAsync(project, elapsed, markerType, url, $"User marker: {markerType}");
        await _actionLogger.LogAsync("manual-marker", url ?? string.Empty, $"User marker: {markerType}", new { markerType });
    }

    private async Task PauseAutomationAsync()
    {
        _hadManualTakeover = true;
        if (_browserController is not null)
        {
            await _browserController.PauseAutoObserveAsync();
        }

        await _actionLogger.LogAsync("manual-control-start", "", "人工接管中：自动观察已暂停，录屏继续记录你的人工操作。");
        SetWorkflowState(WorkflowState.ManualControl);
        _floatingWindow?.SetState("人工接管中：自动观察已暂停，录屏继续");
    }

    private async Task ResumeAutomationAsync()
    {
        _logger.Info("即将恢复自动观察，请确保浏览器位于前台。");
        _floatingWindow?.SetState("3 秒后继续自动");
        await ShowCountdownAsync("继续自动");

        if (_browserController is not null)
        {
            await _browserController.ResumeAutoObserveAsync();
        }

        await _actionLogger.LogAsync("manual-control-end", "", "结束人工接管");
        await _actionLogger.LogAsync("auto-resume", "", "继续自动观察");
        SetWorkflowState(WorkflowState.AutoObserving);
        _floatingWindow?.SetState("自动中");
    }

    private async Task StopAutomationAsync()
    {
        _autoObserveCancellation?.Cancel();
        if (_browserController is not null)
        {
            await _browserController.StopAutoObserveAsync();
        }

        if (_autoObserveTask is not null)
        {
            try
            {
                await Task.WhenAny(_autoObserveTask, Task.Delay(1000));
            }
            catch
            {
                // Cancellation and automation errors are already reflected in the log.
            }
        }
    }

    private async Task RunAutoObserveAsync(ActionProfile profile, CancellationToken token)
    {
        try
        {
            if (_browserController is not null)
            {
                await _browserController.StartAutoObserveAsync(profile, token);
            }
        }
        catch (OperationCanceledException)
        {
            _logger.Info("自动观察已取消。");
        }
        catch (Exception ex)
        {
            _logger.Error("自动观察失败。", ex);
        }
    }

    private async Task CollectInteractionTargetsForCurrentPageAsync(RebuildProject project, ActionProfile profile)
    {
        if (!profile.EnableDomTargetCollection)
        {
            return;
        }

        if (!Uri.TryCreate(NormalizeUrl(UrlBox.Text), UriKind.Absolute, out var baseUri)
            && !Uri.TryCreate(NormalizeUrl(project.ReferenceUrl), UriKind.Absolute, out baseUri))
        {
            baseUri = new Uri("https://example.com/");
        }

        var result = await _domTargetCollector.CollectAsync(_browserController, baseUri);
        await _domTargetCollector.SaveAsync(project, result);

        profile.RuntimeInteractionCollectionStatus = result.Status;
        profile.RuntimeInteractionTargets = result.Targets
            .Where(target => target.Priority > 0)
            .Take(Math.Clamp(profile.MaxTargetsPerViewport, 1, 20))
            .ToList();

        await _actionLogger.LogAsync(
            "dom-target-collection",
            result.Status,
            result.Reason ?? "网页交互目标采集完成。",
            new
            {
                count = result.Targets.Count,
                fallback = result.Fallback
            });
    }

    private async Task ShowCountdownAsync(string finalText)
    {
        SetWorkflowState(WorkflowState.Countdown);
        var countdown = new CountdownWindow();
        await countdown.RunAsync(finalText);
    }

    private async Task EmergencyStopAutomationAsync()
    {
        _hadManualTakeover = true;
        await StopAutomationAsync();
        _manualMouseDetectionTimer.Stop();
        _floatingWindow?.SetState("自动化已停止");
        CurrentStatusText.Text = "当前状态：自动化已停止";
    }

    private async Task CheckToolsAsync()
    {
        var ffmpeg = await ReadToolVersionAsync(FfmpegPathBox.Text.Trim(), "FFmpeg");
        var ffprobe = await ReadToolVersionAsync(FfprobePathBox.Text.Trim(), "FFprobe");
        var message = ffmpeg + Environment.NewLine + ffprobe;
        _logger.Info(message.Replace(Environment.NewLine, " | "));
        System.Windows.MessageBox.Show(message, "工具检测", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private static async Task<string> ReadToolVersionAsync(string path, string label)
    {
        FfmpegScreenRecorder.EnsureExecutableLooksValid(path);
        var startInfo = new ProcessStartInfo
        {
            FileName = path,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        startInfo.ArgumentList.Add("-version");

        using var process = Process.Start(startInfo) ?? throw new InvalidOperationException($"{label} 启动失败。");
        var output = await process.StandardOutput.ReadLineAsync();
        await process.WaitForExitAsync();
        return $"{label}: {output}";
    }

    private async Task<RebuildProject> EnsureProjectAsync()
    {
        if (_project is not null)
        {
            return _project;
        }

        await Task.CompletedTask;
        throw new InvalidOperationException("请先新建或打开一个项目。");
    }

    private async Task SaveProfilesAsync()
    {
        if (_project is null)
        {
            return;
        }

        SyncProjectSettingsFromUi(_project);
        await _projectService.SaveBrowserProfileAsync(_project, BuildBrowserProfile());
        await _projectService.SaveToolProfileAsync(_project, new ToolProfile
        {
            FfmpegPath = FfmpegPathBox.Text.Trim(),
            FfprobePath = FfprobePathBox.Text.Trim()
        });
        _activeActionProfile = ApplySpeedPreset(await _projectService.LoadActionProfileAsync(_project), GetSelectedSpeedPreset());
        _activeActionProfile.ObserveMode = GetSelectedObserveMode();
        _activeActionProfile.TryClickHotspots = TryClickHotspotsCheckBox.IsChecked == true;
        _activeActionProfile.EnableDomTargetCollection = CollectDomTargetsCheckBox.IsChecked == true;
        _activeActionProfile.EnableSafeClick = SafeClickTargetsCheckBox.IsChecked == true;
        _activeActionProfile.MaxTargetsPerViewport = ParseInt(MaxDomTargetsBox.Text, 1, 20, "每屏交互目标数");
        await _projectService.SaveActionProfileAsync(_project, _activeActionProfile);
        await _projectService.SaveCurrentProjectAsync();
    }

    private async Task<ActionProfile> LoadActionProfileForCurrentUiAsync()
    {
        var project = _project ?? throw new InvalidOperationException("请先新建项目。");
        var profile = await _projectService.LoadActionProfileAsync(project);
        profile = ApplySpeedPreset(profile, GetSelectedSpeedPreset());
        profile.ObserveMode = GetSelectedObserveMode();
        profile.TryClickHotspots = TryClickHotspotsCheckBox.IsChecked == true;
        profile.EnableDomTargetCollection = CollectDomTargetsCheckBox.IsChecked == true;
        profile.EnableSafeClick = SafeClickTargetsCheckBox.IsChecked == true;
        profile.MaxTargetsPerViewport = ParseInt(MaxDomTargetsBox.Text, 1, 20, "每屏交互目标数");
        await _projectService.SaveActionProfileAsync(project, profile);
        _activeActionProfile = profile;
        return profile;
    }

    private ActionProfile ApplySpeedPreset(ActionProfile profile, string preset)
    {
        profile.SpeedPreset = preset;
        switch (preset)
        {
            case "turbo":
                profile.MouseMoveDurationMsMin = 120;
                profile.MouseMoveDurationMsMax = 280;
                profile.HoverDurationMsMin = 150;
                profile.HoverDurationMsMax = 350;
                profile.ScrollDistancePxMin = 900;
                profile.ScrollDistancePxMax = 1500;
                profile.ScrollPauseMsMin = 120;
                profile.ScrollPauseMsMax = 300;
                profile.AutoObserveDurationSeconds = 30;
                profile.MaxAutoScrollSteps = 12;
                break;
            case "fast":
                profile.MouseMoveDurationMsMin = 180;
                profile.MouseMoveDurationMsMax = 420;
                profile.HoverDurationMsMin = 300;
                profile.HoverDurationMsMax = 700;
                profile.ScrollDistancePxMin = 650;
                profile.ScrollDistancePxMax = 1200;
                profile.ScrollPauseMsMin = 250;
                profile.ScrollPauseMsMax = 600;
                profile.AutoObserveDurationSeconds = 55;
                profile.MaxAutoScrollSteps = 16;
                break;
            case "detailed":
                profile.MouseMoveDurationMsMin = 500;
                profile.MouseMoveDurationMsMax = 1100;
                profile.HoverDurationMsMin = 1000;
                profile.HoverDurationMsMax = 2200;
                profile.ScrollDistancePxMin = 300;
                profile.ScrollDistancePxMax = 650;
                profile.ScrollPauseMsMin = 900;
                profile.ScrollPauseMsMax = 1800;
                profile.AutoObserveDurationSeconds = 120;
                profile.MaxAutoScrollSteps = 25;
                break;
            default:
                profile.SpeedPreset = "standard";
                profile.MouseMoveDurationMsMin = 300;
                profile.MouseMoveDurationMsMax = 700;
                profile.HoverDurationMsMin = 600;
                profile.HoverDurationMsMax = 1200;
                profile.ScrollDistancePxMin = 450;
                profile.ScrollDistancePxMax = 900;
                profile.ScrollPauseMsMin = 500;
                profile.ScrollPauseMsMax = 1000;
                profile.AutoObserveDurationSeconds = 75;
                profile.MaxAutoScrollSteps = 20;
                break;
        }

        return profile;
    }

    private string GetSelectedSpeedPreset()
    {
        return AutoObserveSpeedCombo.SelectedItem is ComboBoxItem { Tag: string tag } ? tag : "fast";
    }

    private void SetSpeedComboByPreset(string preset)
    {
        if (AutoObserveSpeedCombo is null)
        {
            return;
        }

        foreach (var item in AutoObserveSpeedCombo.Items.OfType<ComboBoxItem>())
        {
            if (string.Equals(item.Tag?.ToString(), preset, StringComparison.OrdinalIgnoreCase))
            {
                AutoObserveSpeedCombo.SelectedItem = item;
                return;
            }
        }

        AutoObserveSpeedCombo.SelectedIndex = 1;
    }

    private string GetSelectedObserveMode()
    {
        return AutoObserveModeCombo.SelectedItem is ComboBoxItem { Tag: string tag } ? tag : ObserveModes.HotspotPriority;
    }

    private void SetObserveModeComboByMode(string mode)
    {
        if (AutoObserveModeCombo is null)
        {
            return;
        }

        foreach (var item in AutoObserveModeCombo.Items.OfType<ComboBoxItem>())
        {
            if (string.Equals(item.Tag?.ToString(), mode, StringComparison.OrdinalIgnoreCase))
            {
                AutoObserveModeCombo.SelectedItem = item;
                return;
            }
        }

        AutoObserveModeCombo.SelectedIndex = 2;
    }

    private RecordingStartMode GetSelectedRecordingStartMode()
    {
        if (RecordingStartModeCombo.SelectedItem is ComboBoxItem { Tag: string tag }
            && Enum.TryParse<RecordingStartMode>(tag, out var mode))
        {
            return mode;
        }

        return RecordingStartMode.RecordBeforeOpenUrl;
    }

    private void SetRecordingStartModeCombo(RecordingStartMode mode)
    {
        if (RecordingStartModeCombo is null)
        {
            return;
        }

        foreach (var item in RecordingStartModeCombo.Items.OfType<ComboBoxItem>())
        {
            if (string.Equals(item.Tag?.ToString(), mode.ToString(), StringComparison.OrdinalIgnoreCase))
            {
                RecordingStartModeCombo.SelectedItem = item;
                return;
            }
        }

        RecordingStartModeCombo.SelectedIndex = 1;
    }

    private static string ToSpeedPresetDisplay(string preset)
    {
        return preset switch
        {
            "safe" => "稳妥",
            "fast" => "快速",
            "custom" => "自定义",
            _ => "标准"
        };
    }

    private async Task RefreshRecentProjectsAsync()
    {
        var projects = await _projectService.LoadRecentProjectsAsync();
        RecentProjectsListBox.ItemsSource = projects;
        StartupRecentProjectsListBox.ItemsSource = projects;
    }

    private ProjectHistoryItem? GetSelectedRecentProject()
    {
        return StartupPageGrid.Visibility == Visibility.Visible
            ? StartupRecentProjectsListBox.SelectedItem as ProjectHistoryItem
            : RecentProjectsListBox.SelectedItem as ProjectHistoryItem;
    }

    private async Task OpenSelectedRecentProjectAsync()
    {
        if (GetSelectedRecentProject() is not ProjectHistoryItem item)
        {
            return;
        }

        await OpenProjectDirectoryCoreAsync(item.ProjectDirectory);
    }

    private async Task OpenExistingProjectDirectoryAsync()
    {
        using var dialog = new WinForms.FolderBrowserDialog
        {
            Description = "选择已有 WebRebuildRecorder 项目目录",
            SelectedPath = Directory.Exists(OutputRootBox.Text) ? OutputRootBox.Text : _projectService.GetFallbackRootDirectory(),
            UseDescriptionForTitle = true
        };

        if (dialog.ShowDialog() != WinForms.DialogResult.OK)
        {
            return;
        }

        var selectedPath = dialog.SelectedPath;
        if (ProjectService.IsValidProjectDirectory(selectedPath))
        {
            await OpenProjectDirectoryCoreAsync(selectedPath);
            return;
        }

        var childProjects = ProjectService.FindChildProjectDirectories(selectedPath);
        if (childProjects.Count == 1)
        {
            var result = System.Windows.MessageBox.Show(
                $"当前目录不是具体项目目录，但发现一个项目子目录：\n{childProjects[0]}\n\n是否打开该项目？",
                "打开项目",
                MessageBoxButton.OKCancel,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.OK)
            {
                await OpenProjectDirectoryCoreAsync(childProjects[0]);
            }

            return;
        }

        if (childProjects.Count > 1)
        {
            System.Windows.MessageBox.Show(
                "当前目录不是具体项目目录，但其下包含多个项目。请进入具体项目目录后再打开。",
                "打开项目",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        System.Windows.MessageBox.Show(
            "该目录不是有效的 WebRebuildRecorder 项目目录。请选择包含 project.json 或 project-info.json 的具体项目目录。",
            "打开项目",
            MessageBoxButton.OK,
            MessageBoxImage.Warning);
    }

    private async Task OpenProjectDirectoryCoreAsync(string projectDirectory)
    {
        var project = await _projectService.OpenProjectAsync(projectDirectory);
        _project = project;
        ResetRuntimeStateForProject(project);
        await ApplyProjectSettingsToUiAsync(project);
        BindProjectToUi(project);
        _aiSiteWorkflowService.EnsureAiSiteProjectStructure(project);
        _userIntentAssetService.EnsureStructure(project);
        UpdateAssetManifestStatus(project);
        UpdateUserIntentImageCounts(project);
        await LoadRunUiStateAsync(project);
        await RefreshRecentProjectsAsync();
        SetWorkflowState(WorkflowState.ProjectCreated);
        ProjectReadyText.Text = "项目已打开，运行状态已重置。";
    }

    private async Task RemoveSelectedRecentProjectAsync()
    {
        if (GetSelectedRecentProject() is not ProjectHistoryItem item)
        {
            return;
        }

        var result = System.Windows.MessageBox.Show(
            "仅从最近项目列表移除，不会删除项目文件。",
            "从列表移除",
            MessageBoxButton.OKCancel,
            MessageBoxImage.Information);
        if (result != MessageBoxResult.OK)
        {
            return;
        }

        await _projectService.RemoveRecentProjectAsync(item.ProjectDirectory);
        await RefreshRecentProjectsAsync();
    }

    private async Task CloseCurrentProjectAsync()
    {
        if (_recorder.IsRecording)
        {
            throw new InvalidOperationException("请先停止录屏，再关闭项目。");
        }

        if (_project is not null)
        {
            await SaveProfilesAsync();
        }

        CloseDetachedPreviewWindow();
        await _projectService.CloseCurrentProjectAsync();
        _project = null;
        ClearRuntimeState();
        _actionLogger.DetachProject();
        _markerService.ClearRuntime();
        ProjectNameBox.Text = string.Empty;
        UrlBox.Text = string.Empty;
        LocalCodePathBox.Text = string.Empty;
        AssetSourceDirectoryBox.Text = string.Empty;
        ProjectReadyText.Text = "请先新建或打开一个项目。";
        ProjectDirectoryText.Text = "--";
        SetWorkflowState(WorkflowState.Idle);
        await RefreshRecentProjectsAsync();
    }

    private void ResetRuntimeStateForProject(RebuildProject project)
    {
        ClearRuntimeState();
        _actionLogger.ClearRuntime();
        _actionLogger.AttachProject(project);
        _actionLogger.SetSessionStart(DateTime.Now);
        _markerService.ResetForProject(project);
        _currentRunId = string.IsNullOrWhiteSpace(project.LastRunId) ? null : project.LastRunId;
        _useFallbackForCodex = false;
    }

    private void ClearRuntimeState()
    {
        _recordingInfo = null;
        _frameResult = null;
        _frameOptions = null;
        _recordingStart = DateTime.MinValue;
        _recordingEnd = DateTime.MinValue;
        _openUrlTime = null;
        _currentRecordingStartMode = RecordingStartMode.RecordBeforeOpenUrl;
        _currentRecordingStartedBeforeOpenUrl = false;
        _lastMousePromptTime = DateTime.MinValue;
        _manualMouseAnomalyCount = 0;
        _lastObservedMousePosition = default;
        _hadManualTakeover = false;
        _websiteOpened = false;
        _currentRecordingIsAutoObserve = false;
        _manualScreenshotCounter = 0;
        _estimatedFrameCount = 0;
        _activeExtractionDirectory = null;
        _observationMarkdownPath = null;
        _framesZipPath = null;
        _observationPackagePath = null;
        _chatGptPackageResult = null;
        _codexPackageResult = null;
        _assetRequirementReport = null;
        _currentRunId = null;
        _lastEmbeddedPreviewUri = null;
        _useFallbackForCodex = false;
        EmbeddedPreviewStatusText.Text = "WebView2 预览：尚未启动";
        SourceSnapshotStatusText.Text = "Source Snapshot：尚未生成";
        RecordingStateText.Text = "录屏：未开始";
        FramesCountText.Text = "截图数量：--";
        VideoDurationText.Text = "时长：--";
        VideoSizeText.Text = "文件大小：--";
        ResolutionText.Text = "分辨率：--";
        ChatGptPackageText.Text = "ChatGPT 包：尚未生成";
        AssetManifestStatusText.Text = "素材清单：尚未加载";
        AssetRequirementsText.Text = "素材需求：尚未导入 GPT 结果";
        FinalCodexPackageText.Text = "最终 Codex 包：尚未生成";
        FirstImpressionBox.Text = string.Empty;
        FavoritePartsBox.Text = string.Empty;
        TargetEffectsBox.Text = string.Empty;
        AvoidPartsBox.Text = string.Empty;
        DesiredOutcomeBox.Text = string.Empty;
        UserIntentStatusText.Text = "可填写主观意图；未填写时也会使用默认说明生成 GPT 分析包。";
        UpdateUserIntentImageCounts(null);
        ObservationText.Text = "抽帧后可生成 observation.md、GPT 分析包和最终 Codex 任务包。";
        ExtractEstimateText.Text = "尚未录屏。";
    }

    private void BindProjectToUi(RebuildProject project)
    {
        ProjectNameBox.Text = project.ProjectName;
        UrlBox.Text = project.ReferenceUrl;
        OutputRootBox.Text = project.RootDirectory;
        LocalCodePathBox.Text = project.LocalCodeProjectPath;
        AssetSourceDirectoryBox.Text = project.UserAssetSourceDirectory;
        ProjectDirectoryText.Text = project.ProjectDirectory;
    }

    private async Task ApplyProjectSettingsToUiAsync(RebuildProject project)
    {
        var browserProfile = await _projectService.LoadBrowserProfileAsync(project);
        var actionProfile = await _projectService.LoadActionProfileAsync(project);
        var toolProfile = await _projectService.LoadToolProfileAsync(project);

        if (!string.IsNullOrWhiteSpace(toolProfile.FfmpegPath))
        {
            FfmpegPathBox.Text = toolProfile.FfmpegPath;
        }

        if (!string.IsNullOrWhiteSpace(toolProfile.FfprobePath))
        {
            FfprobePathBox.Text = toolProfile.FfprobePath;
        }

        RecordingFpsBox.Text = project.RecordingSettings.FrameRate.ToString();
        RecordingCrfBox.Text = project.RecordingSettings.Crf.ToString();
        IntervalBox.Text = project.FrameExtractSettings.IntervalMs.ToString();
        QualityBox.Text = project.FrameExtractSettings.ImageQuality.ToString();
        SetComboByContent(FormatCombo, project.FrameExtractSettings.ImageFormat);
        SetComboByContent(BrowserCombo, string.IsNullOrWhiteSpace(project.BrowserSettings.Browser) ? browserProfile.DefaultBrowser : project.BrowserSettings.Browser);
        SetComboByContent(WindowSizePresetBox, project.BrowserSettings.WindowPreset);
        SetSpeedComboByPreset(string.IsNullOrWhiteSpace(project.AutoObserveSettings.SpeedPreset) ? actionProfile.SpeedPreset : project.AutoObserveSettings.SpeedPreset);
        SetObserveModeComboByMode(string.IsNullOrWhiteSpace(project.AutoObserveSettings.ObserveMode) ? actionProfile.ObserveMode : project.AutoObserveSettings.ObserveMode);
        TryClickHotspotsCheckBox.IsChecked = project.AutoObserveSettings.TryClickHotspots || actionProfile.TryClickHotspots;
        CollectDomTargetsCheckBox.IsChecked = project.AutoObserveSettings.EnableDomTargetCollection || actionProfile.EnableDomTargetCollection;
        SafeClickTargetsCheckBox.IsChecked = project.AutoObserveSettings.EnableSafeClick || actionProfile.EnableSafeClick;
        MaxDomTargetsBox.Text = Math.Clamp(
            project.AutoObserveSettings.MaxTargetsPerViewport > 0
                ? project.AutoObserveSettings.MaxTargetsPerViewport
                : actionProfile.MaxTargetsPerViewport,
            1,
            20).ToString();
        SetRecordingStartModeCombo(project.RecordingSettings.RecordingStartMode);
        if (string.Equals(project.RecordingSettings.RecordingMode, RecordingAreaModes.Region, StringComparison.OrdinalIgnoreCase))
        {
            RegionRadio.IsChecked = true;
        }
        else
        {
            FullScreenRadio.IsChecked = true;
        }
    }

    private void SyncProjectSettingsFromUi(RebuildProject project)
    {
        project.ProjectName = ProjectNameBox.Text.Trim();
        project.ReferenceUrl = UrlBox.Text.Trim();
        project.RootDirectory = OutputRootBox.Text.Trim();
        project.LocalCodeProjectPath = LocalCodePathBox.Text.Trim();
        project.UserAssetSourceDirectory = AssetSourceDirectoryBox.Text.Trim();
        project.RecordingSettings.RecordingMode = RegionRadio.IsChecked == true ? RecordingAreaModes.Region : RecordingAreaModes.FullScreen;
        project.RecordingSettings.FrameRate = ParseInt(RecordingFpsBox.Text, 1, 120, "frame rate");
        project.RecordingSettings.Crf = ParseInt(RecordingCrfBox.Text, 0, 51, "CRF");
        project.RecordingSettings.RecordingStartMode = GetSelectedRecordingStartMode();
        project.FrameExtractSettings.IntervalMs = ParseInt(IntervalBox.Text, 1, 1000, "frame interval");
        project.FrameExtractSettings.ImageFormat = GetComboText(FormatCombo);
        project.FrameExtractSettings.ImageQuality = ParseInt(QualityBox.Text, 1, 100, "image quality");
        project.BrowserSettings.Browser = GetComboText(BrowserCombo);
        project.BrowserSettings.WindowPreset = GetComboText(WindowSizePresetBox);
        project.AutoObserveSettings.SpeedPreset = GetSelectedSpeedPreset();
        project.AutoObserveSettings.ObserveMode = GetSelectedObserveMode();
        project.AutoObserveSettings.TryClickHotspots = TryClickHotspotsCheckBox.IsChecked == true;
        project.AutoObserveSettings.EnableDomTargetCollection = CollectDomTargetsCheckBox.IsChecked == true;
        project.AutoObserveSettings.EnableSafeClick = SafeClickTargetsCheckBox.IsChecked == true;
        project.AutoObserveSettings.MaxTargetsPerViewport = ParseInt(MaxDomTargetsBox.Text, 1, 20, "每屏交互目标数");
        project.AutoObserveSettings.MaxDurationSeconds = _activeActionProfile.AutoObserveDurationSeconds;
        project.UpdatedAt = DateTime.Now;
    }

    private BrowserProfile BuildBrowserProfile()
    {
        var (width, height) = ParseWindowSizePreset(GetComboText(WindowSizePresetBox));
        return new BrowserProfile
        {
            DefaultBrowser = GetComboText(BrowserCombo),
            EnablePlaywright = PlaywrightCheckBox.IsChecked == true,
            WindowWidth = width,
            WindowHeight = height,
            UseNewUserDataDirectory = true,
            AllowExternalLinkClick = false,
            RecordingArea = RegionRadio.IsChecked == true && _selectedArea.IsRegion ? _selectedArea : RecordingArea.FullScreen()
        };
    }

    private RecordingArea BuildRecordingArea()
    {
        if (RegionRadio.IsChecked == true)
        {
            if (!_selectedArea.IsRegion)
            {
                throw new InvalidOperationException("请选择录屏区域，或切换为全屏录制。");
            }

            return _selectedArea;
        }

        return RecordingArea.FullScreen();
    }

    private IBrowserController CreateBrowserController(BrowserProfile profile)
    {
        return new PlaywrightBrowserController(_logger, _actionLogger);
    }

    private async Task<RecordingInfo> ProbeRecordingInfoWithFallbackAsync(RebuildProject project, RecordingArea area)
    {
        try
        {
            return await _ffprobeService.ProbeAsync(
                FfprobePathBox.Text.Trim(),
                project.RecordingFilePath,
                _recordingStart,
                _recordingEnd == DateTime.MinValue ? DateTime.Now : _recordingEnd,
                area,
                GetComboText(BrowserCombo),
                _currentRecordingIsAutoObserve,
                _hadManualTakeover);
        }
        catch (Exception ex)
        {
            _logger.Error("FFprobe 读取视频信息失败", ex);
            var fileInfo = File.Exists(project.RecordingFilePath) ? new FileInfo(project.RecordingFilePath) : null;
            return new RecordingInfo
            {
                VideoPath = project.RecordingFilePath,
                StartTime = _recordingStart,
                EndTime = _recordingEnd == DateTime.MinValue ? DateTime.Now : _recordingEnd,
                DurationSeconds = Math.Max(0, ((_recordingEnd == DateTime.MinValue ? DateTime.Now : _recordingEnd) - _recordingStart).TotalSeconds),
                FileSizeBytes = fileInfo?.Length ?? 0,
                RecordingArea = area,
                Browser = GetComboText(BrowserCombo),
                IsAutoObserve = _currentRecordingIsAutoObserve,
                HadManualTakeover = _hadManualTakeover
            };
        }
    }

    private void ApplyRecordingMetadata(RecordingInfo info)
    {
        info.RecordingStartMode = _currentRecordingStartMode;
        info.RecordingStartedBeforeOpenUrl = _currentRecordingStartedBeforeOpenUrl;
        info.OpenUrlTime = _openUrlTime;
        info.IncludesInitialLoadAnimation = _currentRecordingStartedBeforeOpenUrl && _openUrlTime is not null && info.StartTime <= _openUrlTime.Value;
        info.UsedFloatingToolbar = _floatingWindow is not null;
        info.FloatingToolbarMode = _floatingWindow?.ToolbarMode ?? _appSettings.FloatingToolbar.Mode;
        info.FloatingToolbarLeft = _floatingWindow?.Left ?? _appSettings.FloatingToolbar.Left ?? 0;
        info.FloatingToolbarTop = _floatingWindow?.Top ?? _appSettings.FloatingToolbar.Top;
        info.FloatingToolbarMayOcclude = info.UsedFloatingToolbar && info.FloatingToolbarTop < 120;
    }

    private void ShowFloatingWindow(string state)
    {
        if (_floatingWindow is not null)
        {
            _floatingWindow.SetState(state);
            _floatingWindow.SetSpeedPreset(ToSpeedPresetDisplay(GetSelectedSpeedPreset()));
            _floatingWindow.Show();
            return;
        }

        _floatingWindow = new FloatingRecorderWindow
        {
            IntervalMs = ParseInt(IntervalBox.Text, 1, 1000, "抽帧间隔"),
            AutoExtractAfterStop = AutoExtractAfterStopCheckBox.IsChecked == true
        };
        _floatingWindow.Configure(
            _appSettings.FloatingToolbar.Left,
            _appSettings.FloatingToolbar.Top,
            _appSettings.FloatingToolbar.OpacityIdle,
            _appSettings.FloatingToolbar.OpacityHover,
            _appSettings.FloatingToolbar.Mode);
        _floatingWindow.AutoRecordRequested += async (_, _) => await SafeRunAsync(StartAutomaticClosedLoopAsync);
        _floatingWindow.ManualRequested += async (_, _) => await SafeRunAsync(PauseAutomationAsync);
        _floatingWindow.ResumeRequested += async (_, _) => await SafeRunAsync(ResumeAutomationAsync);
        _floatingWindow.MarkerRequested += async (_, _) => await SafeRunAsync(AddManualMarkerAsync);
        _floatingWindow.ScreenshotRequested += async (_, _) => await SafeRunAsync(CaptureManualScreenshotAsync);
        _floatingWindow.StopRequested += async (_, _) => await SafeRunAsync(StopRecordingAsync);
        _floatingWindow.OpenDirectoryRequested += (_, _) =>
        {
            if (_project is not null)
            {
                OpenDirectory(_project.ProjectDirectory);
            }
        };
        _floatingWindow.DeliveryRequested += (_, _) => ShowCompletionPrompt();
        _floatingWindow.ToolbarLayoutChanged += async (_, _) => await SafeRunAsync(SaveAppSettingsAsync);
        _floatingWindow.EmergencyStopRequested += async (_, _) => await SafeRunAsync(EmergencyStopAutomationAsync);
        _floatingWindow.IntervalChanged += (_, interval) =>
        {
            IntervalBox.Text = Math.Clamp(interval, 1, 1000).ToString();
            UpdateFrameEstimateText();
        };
        _floatingWindow.AutoExtractChanged += (_, value) => AutoExtractAfterStopCheckBox.IsChecked = value;
        _floatingWindow.ManualPromptAcceptRequested += async (_, _) =>
        {
            _manualPromptHideTimer.Stop();
            _floatingWindow?.HideManualTakeoverPrompt();
            await SafeRunAsync(PauseAutomationAsync);
        };
        _floatingWindow.ManualPromptIgnoreRequested += async (_, _) =>
        {
            _manualPromptHideTimer.Stop();
            _floatingWindow?.HideManualTakeoverPrompt();
            await SafeRunAsync(async () => await _actionLogger.LogAsync("manual-control-ignore", "", "用户选择继续自动观察"));
        };
        _floatingWindow.SetState(state);
        _floatingWindow.SetSpeedPreset(ToSpeedPresetDisplay(GetSelectedSpeedPreset()));
        _floatingWindow.Show();
    }

    private void CloseFloatingWindow()
    {
        if (_floatingWindow is null)
        {
            return;
        }

        _floatingWindow.Close();
        _floatingWindow = null;
    }

    private void RecordingTimer_Tick(object? sender, EventArgs e)
    {
        if (!_recorder.IsRecording)
        {
            return;
        }

        var elapsed = DateTime.Now - _recordingStart;
        RecordingStateText.Text = $"录屏：进行中 {TimeUtil.FormatTime(elapsed)}";
        _floatingWindow?.SetElapsed(elapsed);
        if (_project is not null && File.Exists(_project.RecordingFilePath))
        {
            _floatingWindow?.SetFileSize(TimeUtil.FormatFileSize(new FileInfo(_project.RecordingFilePath).Length));
        }
    }

    private async void HotKeyService_HotKeyPressed(object? sender, RecorderHotKey action)
    {
        await SafeRunAsync(async () =>
        {
            switch (action)
            {
                case RecorderHotKey.PauseAutomation:
                    await PauseAutomationAsync();
                    break;
                case RecorderHotKey.ResumeAutomation:
                    await ResumeAutomationAsync();
                    break;
                case RecorderHotKey.Marker:
                    await AddManualMarkerAsync();
                    break;
                case RecorderHotKey.Screenshot:
                    await CaptureManualScreenshotAsync();
                    break;
                case RecorderHotKey.StopRecording:
                    await StopRecordingAsync();
                    break;
            }
        });
    }

    private void Logger_LineWritten(object? sender, string line)
    {
        Dispatcher.Invoke(() =>
        {
            LogBox.AppendText(line + Environment.NewLine);
            LogBox.ScrollToEnd();

            if (line.Contains(" action ", StringComparison.OrdinalIgnoreCase)
                || line.Contains(" marker ", StringComparison.OrdinalIgnoreCase))
            {
                ActionLogList.Items.Add(line);
                ActionLogList.ScrollIntoView(line);
            }
        });
    }

    private async void ManualMouseDetectionTimer_Tick(object? sender, EventArgs e)
    {
        if (_workflowState != WorkflowState.AutoObserving)
        {
            _lastObservedMousePosition = NativeInputService.GetCursorPosition();
            return;
        }

        var current = NativeInputService.GetCursorPosition();
        var distance = Math.Sqrt(Math.Pow(current.X - _lastObservedMousePosition.X, 2) + Math.Pow(current.Y - _lastObservedMousePosition.Y, 2));
        var sinceProgrammatic = DateTime.Now - NativeInputService.LastProgrammaticMouseMoveTime;
        var sincePrompt = DateTime.Now - _lastMousePromptTime;
        _lastObservedMousePosition = current;

        if ((DateTime.Now - _recordingStart).TotalSeconds < 2
            || distance < 80
            || sinceProgrammatic.TotalMilliseconds < 800
            || sincePrompt.TotalSeconds < 10)
        {
            _manualMouseAnomalyCount = 0;
            return;
        }

        _manualMouseAnomalyCount++;
        if (_manualMouseAnomalyCount < 2)
        {
            return;
        }

        _manualMouseAnomalyCount = 0;

        _lastMousePromptTime = DateTime.Now;
        await _actionLogger.LogAsync("mouse-manual-detected", "", "检测到鼠标被人工移动，工具条内显示非阻塞人工接管提示。");
        ShowFloatingWindow("检测到人工移动");
        _floatingWindow?.ShowManualTakeoverPrompt();
        _manualPromptHideTimer.Stop();
        _manualPromptHideTimer.Start();
    }

    private void FrameProgressTimer_Tick(object? sender, EventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_activeExtractionDirectory) || !Directory.Exists(_activeExtractionDirectory))
        {
            return;
        }

        var count = Directory.EnumerateFiles(_activeExtractionDirectory, "frame_*.*").Count();
        FramesCountText.Text = $"截图数量：{count}";
        _floatingWindow?.SetExtractionProgress(count, _estimatedFrameCount);
    }

    private void UpdateRecordingInfoUi(RecordingInfo info)
    {
        VideoDurationText.Text = $"视频时长：{TimeUtil.FormatTime(TimeSpan.FromSeconds(info.DurationSeconds))}";
        VideoSizeText.Text = $"文件大小：{TimeUtil.FormatFileSize(info.FileSizeBytes)}";
        ResolutionText.Text = info.Width > 0 && info.Height > 0 ? $"分辨率：{info.Width}x{info.Height}" : "分辨率：--";
    }

    private void UpdateFrameEstimateText()
    {
        if (_recordingInfo is null || _recordingInfo.DurationSeconds <= 0)
        {
            ExtractEstimateText.Text = "视频信息不完整，抽帧前会再次尝试读取。";
            return;
        }

        var interval = ParseInt(IntervalBox.Text, 1, 1000, "抽帧间隔");
        var estimated = _recordingInfo.DurationSeconds * (1000d / interval);
        ExtractEstimateText.Text = $"视频时长 {TimeUtil.FormatTime(TimeSpan.FromSeconds(_recordingInfo.DurationSeconds))}，间隔 {interval} ms，预计 {Math.Ceiling(estimated):0} 张。";
    }

    private void UpdateChatGptPackageUi()
    {
        if (_chatGptPackageResult is null)
        {
            ChatGptPackageText.Text = "ChatGPT 上传包：尚未生成";
            return;
        }

        ChatGptPackageText.Text = "ChatGPT 上传包已生成，只需拖入 ChatGPT 一次即可。" + Environment.NewLine
            + $"路径：{_chatGptPackageResult.ZipPath}" + Environment.NewLine
            + $"大小：{TimeUtil.FormatFileSize(_chatGptPackageResult.ZipSizeBytes)}，精选截图：{_chatGptPackageResult.SelectedFrameCount} 张";
        if (_chatGptPackageResult.ZipSizeBytes > 100L * 1024 * 1024)
        {
            ChatGptPackageText.Text += Environment.NewLine + "强提醒：上传包超过 100MB，建议减少 selected-frames 数量或提高抽帧间隔。";
        }
        else if (_chatGptPackageResult.ZipSizeBytes > 50L * 1024 * 1024)
        {
            ChatGptPackageText.Text += Environment.NewLine + "提示：上传包超过 50MB，上传和分析可能变慢。";
        }

        UpdateButtonStates();
    }

    private static string DefaultShortChatGptPrompt()
    {
        return "请解压我上传的 zip，先阅读 README_FOR_CHATGPT.md 和 observation.md，再结合 selected-frames 分析该网站的页面结构、动效、设计语言，并生成给 Codex 的网页重构指令。";
    }

    private static void CleanFrameOutputDirectory(string directory)
    {
        Directory.CreateDirectory(directory);
        foreach (var file in Directory.EnumerateFiles(directory, "frame_*.*"))
        {
            File.Delete(file);
        }

        foreach (var indexFile in new[] { "frames-index.csv", "frames-index.md" })
        {
            var path = Path.Combine(directory, indexFile);
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    private async Task<string?> GetCurrentUrlOrFallbackAsync()
    {
        if (_browserController is not null)
        {
            var url = await _browserController.GetCurrentUrlAsync();
            if (!string.IsNullOrWhiteSpace(url))
            {
                return url;
            }
        }

        return _project?.ReferenceUrl ?? UrlBox.Text;
    }

    private async Task SafeRunAsync(Func<Task> action)
    {
        try
        {
            await action();
        }
        catch (Exception ex)
        {
            SetWorkflowState(WorkflowState.Error);
            _logger.Error("操作失败，详细异常见下方。", ex);
            var friendlyError = BuildFriendlyError(ex);
            System.Windows.MessageBox.Show(friendlyError.Message, friendlyError.Title, MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private static (string Title, string Message) BuildFriendlyError(Exception ex)
    {
        if (IsProjectFileAccessError(ex))
        {
            return (
                "打开项目失败",
                "打开项目失败。\n\n项目文件可能正在被其他程序占用，或上一次写入尚未完成。\n请关闭其他 WebRebuildRecorder 实例后重试。\n\n详细错误已写入日志。");
        }

        var message = ex.Message.Trim();
        if (!string.IsNullOrWhiteSpace(message) && ContainsChinese(message))
        {
            return ("操作未完成", $"操作未完成。\n\n{message}\n\n详细错误已写入日志。");
        }

        return ("操作未完成", "操作未完成。\n\n详细错误已写入日志。");
    }

    private static bool IsProjectFileAccessError(Exception ex)
    {
        var text = $"{ex.Message} {(ex as IOException)?.Source} {(ex as FileNotFoundException)?.FileName}";
        return ex is IOException or UnauthorizedAccessException or FileNotFoundException
            && (text.Contains("project.json", StringComparison.OrdinalIgnoreCase)
                || text.Contains("project-info.json", StringComparison.OrdinalIgnoreCase));
    }

    private static bool ContainsChinese(string value)
    {
        return value.Any(ch => ch is >= '\u4e00' and <= '\u9fff');
    }

    private void SetWorkflowState(WorkflowState state)
    {
        _workflowState = state;
        CurrentStatusText.Text = $"当前状态：{ToStateText(state)}";
        _floatingWindow?.SetState(ToStateText(state));
        UpdateButtonStates();
    }

    private void UpdateButtonStates()
    {
        if (NewProjectToolbarButton is null)
        {
            return;
        }

        var hasProject = _project is not null;
        var isRecording = _recorder.IsRecording || _workflowState is WorkflowState.Countdown or WorkflowState.Recording or WorkflowState.AutoObserving or WorkflowState.ManualControl;
        var hasRecording = hasProject && _recordingInfo is not null && File.Exists(_project!.RecordingFilePath);
        var hasFrames = _frameResult is not null;
        var canOpenWebsite = hasProject && !isRecording;
        var canStartRecording = hasProject && !isRecording;
        var canStopRecording = _recorder.IsRecording;
        var canExtract = hasRecording && !isRecording;
        var canPackage = hasFrames && !isRecording;
        var canGenerateMd = hasProject && hasRecording && !isRecording;
        var canOpenProject = hasProject;

        UpdateProjectSurface(hasProject);
        NewProjectToolbarButton.IsEnabled = !isRecording;
        OpenExistingProjectToolbarButton.IsEnabled = !isRecording;
        NewProjectWorkflowButton.IsEnabled = !isRecording;
        CloseProjectToolbarButton.IsEnabled = hasProject && !isRecording;
        CloseProjectWorkflowButton.IsEnabled = hasProject && !isRecording;
        OpenRecentProjectButton.IsEnabled = !isRecording;
        RemoveRecentProjectButton.IsEnabled = !isRecording;
        OpenRecentProjectDirectoryButton.IsEnabled = !isRecording;
        StartupOpenRecentButton.IsEnabled = !isRecording;
        StartupOpenRecentDirectoryButton.IsEnabled = !isRecording;
        StartupRemoveRecentButton.IsEnabled = !isRecording;
        OpenWebsiteToolbarButton.IsEnabled = canOpenWebsite;
        OpenWebsiteWorkflowButton.IsEnabled = canOpenWebsite;
        PreviewReferenceInWebViewButton.IsEnabled = hasProject;
        PreviewOutputSiteInWebViewButton.IsEnabled = hasProject;
        RefreshEmbeddedPreviewButton.IsEnabled = hasProject && !string.IsNullOrWhiteSpace(_lastEmbeddedPreviewUri);
        OpenPreviewInExternalBrowserButton.IsEnabled = !string.IsNullOrWhiteSpace(_lastEmbeddedPreviewUri);
        OpenDetachedPreviewWindowButton.IsEnabled = hasProject;
        GenerateSourceSnapshotButton.IsEnabled = hasProject && !isRecording;
        OpenSourceSnapshotDirectoryButton.IsEnabled = hasProject;
        AutoObserveRecordingToolbarButton.IsEnabled = canStartRecording;
        AutoObserveRecordingWorkflowButton.IsEnabled = canStartRecording;
        StartRecordingToolbarButton.IsEnabled = canStartRecording;
        StartRecordingWorkflowButton.IsEnabled = canStartRecording;
        StopRecordingToolbarButton.IsEnabled = canStopRecording;
        StopRecordingWorkflowButton.IsEnabled = canStopRecording;
        ScreenshotWorkflowButton.IsEnabled = hasProject;
        ExtractFramesWorkflowButton.IsEnabled = canExtract;
        PackageWorkflowButton.IsEnabled = canPackage;
        GenerateMarkdownWorkflowButton.IsEnabled = canGenerateMd;
        OpenProjectDirectoryWorkflowButton.IsEnabled = canOpenProject;
        OpenProjectDirectoryWorkflowButton2.IsEnabled = canOpenProject;
        OpenMarkdownDirectoryWorkflowButton.IsEnabled = hasProject;
        GenerateChatGptPackageWorkflowButton.IsEnabled = canPackage;
        CopyChatGptPromptWorkflowButton.IsEnabled = hasProject;
        OpenPackageDirectoryWorkflowButton.IsEnabled = hasProject;
        OpenChatGptWorkflowButton.IsEnabled = hasProject;
        CopyZipPathWorkflowButton.IsEnabled = _chatGptPackageResult is not null;
        ImportAssetsWorkflowButton.IsEnabled = hasProject && !isRecording;
        OpenAssetLibraryWorkflowButton.IsEnabled = hasProject;
        PasteFavoriteImageButton.IsEnabled = hasProject && !isRecording;
        UploadFavoriteImageButton.IsEnabled = hasProject && !isRecording;
        OpenFavoriteImageDirectoryButton.IsEnabled = hasProject;
        ViewUserIntentAssetsManifestButton.IsEnabled = hasProject;
        PasteTargetEffectsImageButton.IsEnabled = hasProject && !isRecording;
        UploadTargetEffectsImageButton.IsEnabled = hasProject && !isRecording;
        OpenTargetEffectsImageDirectoryButton.IsEnabled = hasProject;
        ViewUserIntentAssetsManifestButton2.IsEnabled = hasProject;
        ImportGptOutputWorkflowButton.IsEnabled = hasProject && !isRecording;
        BrowseGptOutputFileWorkflowButton.IsEnabled = hasProject && !isRecording;
        SupplementAssetsWorkflowButton.IsEnabled = hasProject && !isRecording;
        UseFallbackWorkflowButton.IsEnabled = hasProject && _assetRequirementReport is { HasAssetWarning: true };
        GenerateFinalCodexPackageWorkflowButton.IsEnabled = hasProject && !isRecording;
        DisableGlobalHotkeysButton.IsEnabled = _appSettings.Hotkeys?.Enabled == true;
    }

    private void UpdateProjectSurface(bool hasProject)
    {
        StartupPageGrid.Visibility = hasProject ? Visibility.Collapsed : Visibility.Visible;
        WorkbenchGrid.Visibility = hasProject ? Visibility.Visible : Visibility.Collapsed;
        LogGroupBox.Visibility = hasProject ? Visibility.Visible : Visibility.Collapsed;
        LogRow.Height = hasProject ? new GridLength(160) : new GridLength(0);
    }

    private void UpdateHotkeyText()
    {
        if (HotkeyText is null)
        {
            return;
        }

        var hotkeys = _appSettings.Hotkeys ?? new HotkeySettings();
        if (!hotkeys.Enabled)
        {
            HotkeyText.Text = "快捷键状态：全局快捷键已关闭。" + Environment.NewLine
                + "你仍可使用主界面和悬浮工具条按钮。";
            DisableGlobalHotkeysButton.IsEnabled = false;
            return;
        }

        var lines = new List<string>
        {
            "快捷键状态：已启用",
            $"{hotkeys.PauseAuto} 暂停自动 / 人工接管",
            $"{hotkeys.ResumeAuto} 继续自动",
            $"{hotkeys.Marker} 标记重点",
            $"{hotkeys.Screenshot} 当前截图",
            $"{hotkeys.StopRecording} 停止录屏",
            "Esc 仅在窗口有焦点时停止自动观察"
        };

        if (_hotkeyFailures.Count > 0)
        {
            lines.Add("");
            lines.Add("部分全局快捷键被其他程序占用。");
            lines.Add("不影响使用，可直接点击悬浮工具条按钮操作。");
        }

        HotkeyText.Text = string.Join(Environment.NewLine, lines);
        DisableGlobalHotkeysButton.IsEnabled = true;
    }

    private async Task DisableGlobalHotkeysAsync()
    {
        _hotKeyService?.UnregisterAll();
        _appSettings.Hotkeys ??= new HotkeySettings();
        _appSettings.Hotkeys.Enabled = false;
        await _appSettingsService.SaveAsync(_appSettings);
        HotkeyText.Text = "已关闭全局快捷键。你仍可使用主界面和悬浮工具条按钮。";
        DisableGlobalHotkeysButton.IsEnabled = false;
        _logger.Info("用户已关闭全局快捷键。");
    }

    private static string ToStateText(WorkflowState state)
    {
        return state switch
        {
            WorkflowState.Idle => "空闲",
            WorkflowState.ProjectCreated => "项目已创建",
            WorkflowState.WebsiteOpened => "网站已打开",
            WorkflowState.ToolbarReady => "待录屏",
            WorkflowState.Countdown => "倒计时",
            WorkflowState.Recording => "录屏中",
            WorkflowState.AutoObserving => "自动观察中",
            WorkflowState.ManualControl => "人工接管",
            WorkflowState.RecordingStopped => "录屏完成",
            WorkflowState.ExtractingFrames => "抽帧中",
            WorkflowState.Packaging => "打包中",
            WorkflowState.GeneratingMarkdown => "生成观察文档",
            WorkflowState.Completed => "全部完成",
            WorkflowState.Error => "错误",
            _ => state.ToString()
        };
    }

    private AutoFlowOptions BuildAutoFlowOptions()
    {
        return new AutoFlowOptions
        {
            ShowToolbarAfterOpenWebsite = ShowToolbarAfterOpenCheckBox.IsChecked == true,
            ShowCountdownBeforeRecording = ShowCountdownCheckBox.IsChecked == true,
            AutoObservePage = AutoObserveCheckBox.IsChecked == true,
            AutoExtractAfterStop = AutoExtractAfterStopCheckBox.IsChecked == true,
            AutoPackageAfterExtract = AutoPackageAfterExtractCheckBox.IsChecked == true,
            AutoGenerateMarkdownAfterPackage = AutoGenerateMarkdownCheckBox.IsChecked == true,
            ShowCompletionPrompt = ShowCompletePromptCheckBox.IsChecked == true
        };
    }

    private static string NormalizeUrl(string? raw)
    {
        var url = raw?.Trim();
        if (string.IsNullOrWhiteSpace(url))
        {
            throw new InvalidOperationException("请先输入参考网站地址。");
        }

        if (!url.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
            && !url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            url = "https://" + url;
        }

        if (!Uri.TryCreate(url, UriKind.Absolute, out _))
        {
            throw new InvalidOperationException("URL 格式不合法。");
        }

        return url;
    }

    private static int ParseInt(string value, int min, int max, string name)
    {
        if (!int.TryParse(value, out var parsed) || parsed < min || parsed > max)
        {
            throw new InvalidOperationException($"{name} 必须在 {min}-{max} 范围内。");
        }

        return parsed;
    }

    private static int? TryParseNullableInt(string value)
    {
        return int.TryParse(value, out var parsed) && parsed > 0 ? parsed : null;
    }

    private static string GetComboText(System.Windows.Controls.ComboBox comboBox)
    {
        return comboBox.SelectedItem is ComboBoxItem item
            ? item.Tag?.ToString() ?? item.Content?.ToString() ?? string.Empty
            : comboBox.Text;
    }

    private void UpdateProjectDirectoryPreview()
    {
        if (ProjectDirectoryText is null || ProjectNameBox is null || OutputRootBox is null || _projectService is null)
        {
            return;
        }

        if (_project is not null)
        {
            ProjectDirectoryText.Text = _project.ProjectDirectory;
            return;
        }

        var root = string.IsNullOrWhiteSpace(OutputRootBox.Text)
            ? _projectService.GetFallbackRootDirectory()
            : OutputRootBox.Text.Trim();
        var projectName = string.IsNullOrWhiteSpace(ProjectNameBox.Text) ? "project" : ProjectNameBox.Text;

        try
        {
            ProjectDirectoryText.Text = ProjectService.PreviewProjectDirectory(root, projectName);
        }
        catch
        {
            ProjectDirectoryText.Text = "--";
        }
    }

    private static void SetComboByContent(System.Windows.Controls.ComboBox comboBox, string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        foreach (var item in comboBox.Items.OfType<ComboBoxItem>())
        {
            if (string.Equals(item.Tag?.ToString(), value, StringComparison.OrdinalIgnoreCase)
                || string.Equals(item.Content?.ToString(), value, StringComparison.OrdinalIgnoreCase))
            {
                comboBox.SelectedItem = item;
                return;
            }
        }
    }

    private bool IsFallbackDocumentsRoot(string rootDirectory)
    {
        try
        {
            var fallback = Path.GetFullPath(_projectService.GetFallbackRootDirectory()).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            var current = Path.GetFullPath(rootDirectory).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            return string.Equals(fallback, current, StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

#pragma warning disable CS0162
    private void ShowCompletionPrompt()
    {
        if (_project is null)
        {
            return;
        }

        ShowCompletionPanelNonModal();
        return;

        var recordingFile = Path.GetRelativePath(_project.ProjectDirectory, _project.RecordingFilePath);
        var framesDirectory = _frameResult is null ? "--" : Path.GetRelativePath(_project.ProjectDirectory, _frameResult.OutputDirectory);
        var observation = string.IsNullOrWhiteSpace(_observationMarkdownPath) ? "--" : Path.GetRelativePath(_project.ProjectDirectory, _observationMarkdownPath);
        var package = string.IsNullOrWhiteSpace(_observationPackagePath) ? "--" : Path.GetRelativePath(_project.ProjectDirectory, _observationPackagePath);
        var chatGptPackage = string.IsNullOrWhiteSpace(_chatGptPackageResult?.ZipPath) ? "--" : Path.GetRelativePath(_project.ProjectDirectory, _chatGptPackageResult.ZipPath);

        var message = "项目目录：" + Environment.NewLine + _project.ProjectDirectory + Environment.NewLine + Environment.NewLine
            + "录屏文件：" + Environment.NewLine + recordingFile + Environment.NewLine + Environment.NewLine
            + "抽帧目录：" + Environment.NewLine + framesDirectory + Environment.NewLine + Environment.NewLine
            + "观察文档：" + Environment.NewLine + observation + Environment.NewLine + Environment.NewLine
            + "打包文件：" + Environment.NewLine + package + Environment.NewLine + Environment.NewLine
            + "ChatGPT 上传包：" + Environment.NewLine + chatGptPackage;

        var window = new CompletionWindow(message, _project.ProjectDirectory, _project.MarkdownDirectory)
        {
            Owner = this
        };
        window.Show();
    }

#pragma warning restore CS0162
    private void ShowCompletionPanelNonModal()
    {
        if (_project is null)
        {
            return;
        }

        var recordingFile = Path.GetRelativePath(_project.ProjectDirectory, _project.RecordingFilePath);
        var framesDirectory = _frameResult is null ? "--" : Path.GetRelativePath(_project.ProjectDirectory, _frameResult.OutputDirectory);
        var observation = string.IsNullOrWhiteSpace(_observationMarkdownPath) ? "--" : Path.GetRelativePath(_project.ProjectDirectory, _observationMarkdownPath);
        var package = string.IsNullOrWhiteSpace(_observationPackagePath) ? "--" : Path.GetRelativePath(_project.ProjectDirectory, _observationPackagePath);
        var chatGptPackage = string.IsNullOrWhiteSpace(_chatGptPackageResult?.ZipPath) ? "--" : Path.GetRelativePath(_project.ProjectDirectory, _chatGptPackageResult.ZipPath);

        var message = "任务已完成" + Environment.NewLine + Environment.NewLine
            + "项目目录：" + Environment.NewLine + _project.ProjectDirectory + Environment.NewLine + Environment.NewLine
            + "录屏文件：" + Environment.NewLine + recordingFile + Environment.NewLine + Environment.NewLine
            + "抽帧目录：" + Environment.NewLine + framesDirectory + Environment.NewLine + Environment.NewLine
            + "observation.md:" + Environment.NewLine + observation + Environment.NewLine + Environment.NewLine
            + "观察资料包：" + Environment.NewLine + package + Environment.NewLine + Environment.NewLine
            + "ChatGPT 上传包：" + Environment.NewLine + chatGptPackage;

        _completionWindow?.Close();
        _completionWindow = new CompletionWindow(
            message,
            _project.ProjectDirectory,
            GetCurrentPackageDirectory(_project),
            _chatGptPackageResult?.ZipPath ?? string.Empty,
            _chatGptPackageResult?.PromptText ?? DefaultShortChatGptPrompt())
        {
            Owner = this
        };
        _completionWindow.Closed += (_, _) => _completionWindow = null;
        _completionWindow.Show();
        CurrentStatusText.Text = "当前状态：任务完成";
    }

    private static (int Width, int Height) ParseWindowSizePreset(string preset)
    {
        if (preset.Contains("1920x1080", StringComparison.OrdinalIgnoreCase))
        {
            return (1920, 1080);
        }

        if (preset.Contains("1366x768", StringComparison.OrdinalIgnoreCase))
        {
            return (1366, 768);
        }

        if (preset.Contains("768x1024", StringComparison.OrdinalIgnoreCase))
        {
            return (768, 1024);
        }

        if (preset.Contains("390x844", StringComparison.OrdinalIgnoreCase))
        {
            return (390, 844);
        }

        return (1440, 900);
    }

    private string GetCurrentPackageDirectory(RebuildProject project)
    {
        if (!string.IsNullOrWhiteSpace(_chatGptPackageResult?.ZipPath))
        {
            var directory = Path.GetDirectoryName(_chatGptPackageResult.ZipPath);
            if (!string.IsNullOrWhiteSpace(directory) && Directory.Exists(directory))
            {
                return directory;
            }
        }

        if (!string.IsNullOrWhiteSpace(_codexPackageResult?.ZipPath))
        {
            var directory = Path.GetDirectoryName(_codexPackageResult.ZipPath);
            if (!string.IsNullOrWhiteSpace(directory) && Directory.Exists(directory))
            {
                return directory;
            }
        }

        return project.PackageDirectory;
    }

    private static void OpenDirectory(string directory)
    {
        if (!Directory.Exists(directory))
        {
            return;
        }

        Process.Start(new ProcessStartInfo
        {
            FileName = directory,
            UseShellExecute = true
        });
    }
}
