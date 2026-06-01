using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace WebRebuildRecorder.App.ViewModels;

public sealed class MainWindowViewModel : INotifyPropertyChanged
{
    private string _status = "空闲";
    private string _projectName = "hskin-offmenu";
    private string _referenceUrl = string.Empty;
    private string _projectsRootDirectory = string.Empty;

    public event PropertyChangedEventHandler? PropertyChanged;

    public ObservableCollection<string> LogLines { get; } = [];
    public ObservableCollection<string> ActionLines { get; } = [];

    public string Status
    {
        get => _status;
        set
        {
            if (_status == value)
            {
                return;
            }

            _status = value;
            OnPropertyChanged();
        }
    }

    public string ProjectName
    {
        get => _projectName;
        set
        {
            if (_projectName == value)
            {
                return;
            }

            _projectName = value;
            OnPropertyChanged();
        }
    }

    public string ReferenceUrl
    {
        get => _referenceUrl;
        set
        {
            if (_referenceUrl == value)
            {
                return;
            }

            _referenceUrl = value;
            OnPropertyChanged();
        }
    }

    public string ProjectsRootDirectory
    {
        get => _projectsRootDirectory;
        set
        {
            if (_projectsRootDirectory == value)
            {
                return;
            }

            _projectsRootDirectory = value;
            OnPropertyChanged();
        }
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
