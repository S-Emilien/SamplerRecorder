# Technical Architecture

<cite>
**Referenced Files in This Document**
- [App.xaml.cs](file://App.xaml.cs)
- [MainWindow.xaml.cs](file://MainWindow.xaml.cs)
- [MainViewModel.cs](file://ViewModels/MainViewModel.cs)
- [ClipItemViewModel.cs](file://ViewModels/ClipItemViewModel.cs)
- [AudioCaptureService.cs](file://Services/AudioCaptureService.cs)
- [AudioExportService.cs](file://Services/AudioExportService.cs)
- [HotkeyService.cs](file://Services/HotkeyService.cs)
- [SessionStore.cs](file://Services/SessionStore.cs)
- [SettingsService.cs](file://Services/SettingsService.cs)
- [WaveformDataService.cs](file://Services/WaveformDataService.cs)
- [AppSettings.cs](file://Models/AppSettings.cs)
- [AudioClip.cs](file://Models/AudioClip.cs)
- [Marker.cs](file://Models/Marker.cs)
- [RecordingSession.cs](file://Models/RecordingSession.cs)
- [WaveformControl.cs](file://Controls/WaveformControl.cs)
- [DarkTheme.xaml](file://Themes/DarkTheme.xaml)
</cite>

## Table of Contents
1. [Introduction](#introduction)
2. [Project Structure](#project-structure)
3. [Core Components](#core-components)
4. [Architecture Overview](#architecture-overview)
5. [Detailed Component Analysis](#detailed-component-analysis)
6. [Dependency Analysis](#dependency-analysis)
7. [Performance Considerations](#performance-considerations)
8. [Troubleshooting Guide](#troubleshooting-guide)
9. [Conclusion](#conclusion)

## Introduction
SamplerRecorder is a WPF-based audio recording and sampling application that implements the Model-View-ViewModel (MVVM) architectural pattern. The application provides functionality for capturing audio, managing recording sessions, visualizing waveforms, and exporting audio clips. The architecture emphasizes separation of concerns, dependency injection, event-driven communication, and maintainable code organization.

The application follows modern WPF development practices with a clear separation between UI logic (Views), business logic (ViewModels), and data access (Services and Models). This design enables testability, scalability, and ease of maintenance while providing a responsive user interface for audio processing tasks.

## Project Structure
The SamplerRecorder application follows a well-organized directory structure that reflects its architectural layers:

```mermaid
graph TB
subgraph "Presentation Layer"
App[App.xaml.cs]
MainWindow[MainWindow.xaml.cs]
WaveformControl[WaveformControl.cs]
DarkTheme[DarkTheme.xaml]
end
subgraph "ViewModel Layer"
MainViewModel[MainViewModel.cs]
ClipItemViewModel[ClipItemViewModel.cs]
end
subgraph "Service Layer"
AudioCaptureService[AudioCaptureService.cs]
AudioExportService[AudioExportService.cs]
HotkeyService[HotkeyService.cs]
SessionStore[SessionStore.cs]
SettingsService[SettingsService.cs]
WaveformDataService[WaveformDataService.cs]
end
subgraph "Model Layer"
AppSettings[AppSettings.cs]
AudioClip[AudioClip.cs]
Marker[Marker.cs]
RecordingSession[RecordingSession.cs]
end
App --> MainWindow
MainWindow --> MainViewModel
MainViewModel --> AudioCaptureService
MainViewModel --> AudioExportService
MainViewModel --> SessionStore
MainViewModel --> SettingsService
MainViewModel --> WaveformDataService
MainViewModel --> ClipItemViewModel
Services --> Models
```

**Diagram sources**
- [App.xaml.cs](file://App.xaml.cs)
- [MainWindow.xaml.cs](file://MainWindow.xaml.cs)
- [MainViewModel.cs](file://ViewModels/MainViewModel.cs)
- [ClipItemViewModel.cs](file://ViewModels/ClipItemViewModel.cs)
- [AudioCaptureService.cs](file://Services/AudioCaptureService.cs)
- [AudioExportService.cs](file://Services/AudioExportService.cs)
- [HotkeyService.cs](file://Services/HotkeyService.cs)
- [SessionStore.cs](file://Services/SessionStore.cs)
- [SettingsService.cs](file://Services/SettingsService.cs)
- [WaveformDataService.cs](file://Services/WaveformDataService.cs)
- [AppSettings.cs](file://Models/AppSettings.cs)
- [AudioClip.cs](file://Models/AudioClip.cs)
- [Marker.cs](file://Models/Marker.cs)
- [RecordingSession.cs](file://Models/RecordingSession.cs)
- [WaveformControl.cs](file://Controls/WaveformControl.cs)
- [DarkTheme.xaml](file://Themes/DarkTheme.xaml)

**Section sources**
- [App.xaml.cs](file://App.xaml.cs)
- [MainWindow.xaml.cs](file://MainWindow.xaml.cs)
- [MainViewModel.cs](file://ViewModels/MainViewModel.cs)
- [ClipItemViewModel.cs](file://ViewModels/ClipItemViewModel.cs)

## Core Components
The SamplerRecorder application consists of several core components that work together to provide audio recording and management functionality:

### View Models
The view models implement the MVVM pattern by exposing properties and commands that bind to the UI while handling business logic and coordinating with services.

### Services Layer
The service layer encapsulates all external dependencies and complex business logic, providing clean interfaces for the view models to interact with audio capture, export functionality, session management, settings, and waveform data processing.

### Models
The model layer represents the domain entities and data structures used throughout the application, including audio clips, recording sessions, markers, and application settings.

### Controls
Custom controls like the waveform control provide specialized UI components for audio visualization and interaction.

**Section sources**
- [MainViewModel.cs](file://ViewModels/MainViewModel.cs)
- [ClipItemViewModel.cs](file://ViewModels/ClipItemViewModel.cs)
- [AudioCaptureService.cs](file://Services/AudioCaptureService.cs)
- [AudioExportService.cs](file://Services/AudioExportService.cs)
- [HotkeyService.cs](file://Services/HotkeyService.cs)
- [SessionStore.cs](file://Services/SessionStore.cs)
- [SettingsService.cs](file://Services/SettingsService.cs)
- [WaveformDataService.cs](file://Services/WaveformDataService.cs)
- [AppSettings.cs](file://Models/AppSettings.cs)
- [AudioClip.cs](file://Models/AudioClip.cs)
- [Marker.cs](file://Models/Marker.cs)
- [RecordingSession.cs](file://Models/RecordingSession.cs)
- [WaveformControl.cs](file://Controls/WaveformControl.cs)

## Architecture Overview
The SamplerRecorder application follows a layered architecture with clear separation of concerns:

```mermaid
graph TD
subgraph "UI Layer"
MainWindow[MainWindow]
WaveformControl[WaveformControl]
DarkTheme[DarkTheme]
end
subgraph "ViewModel Layer"
MainViewModel[MainViewModel]
ClipItemViewModel[ClipItemViewModel]
end
subgraph "Service Layer"
AudioCaptureService[AudioCaptureService]
AudioExportService[AudioExportService]
HotkeyService[HotkeyService]
SessionStore[SessionStore]
SettingsService[SettingsService]
WaveformDataService[WaveformDataService]
end
subgraph "Data Layer"
AppSettings[AppSettings]
AudioClip[AudioClip]
Marker[Marker]
RecordingSession[RecordingSession]
end
MainWindow --> MainViewModel
WaveformControl --> ClipItemViewModel
MainViewModel --> AudioCaptureService
MainViewModel --> AudioExportService
MainViewModel --> HotkeyService
MainViewModel --> SessionStore
MainViewModel --> SettingsService
MainViewModel --> WaveformDataService
Services --> DataLayer
```

**Diagram sources**
- [MainWindow.xaml.cs](file://MainWindow.xaml.cs)
- [MainViewModel.cs](file://ViewModels/MainViewModel.cs)
- [ClipItemViewModel.cs](file://ViewModels/ClipItemViewModel.cs)
- [AudioCaptureService.cs](file://Services/AudioCaptureService.cs)
- [AudioExportService.cs](file://Services/AudioExportService.cs)
- [HotkeyService.cs](file://Services/HotkeyService.cs)
- [SessionStore.cs](file://Services/SessionStore.cs)
- [SettingsService.cs](file://Services/SettingsService.cs)
- [WaveformDataService.cs](file://Services/WaveformDataService.cs)
- [AppSettings.cs](file://Models/AppSettings.cs)
- [AudioClip.cs](file://Models/AudioClip.cs)
- [Marker.cs](file://Models/Marker.cs)
- [RecordingSession.cs](file://Models/RecordingSession.cs)

## Detailed Component Analysis

### MVVM Implementation Pattern
The application implements the MVVM pattern with clear separation between views, view models, and data models:

```mermaid
classDiagram
class MainViewModel {
+RecordingSession CurrentSession
+ObservableCollection~ClipItemViewModel~ Clips
+bool IsRecording
+string StatusMessage
+StartRecording()
+StopRecording()
+ExportSelectedClips()
+DeleteSelectedClips()
+OnPropertyChanged()
}
class ClipItemViewModel {
+AudioClip AudioClip
+bool IsSelected
+string Duration
+DateTime CreatedAt
+Select()
+Deselect()
}
class MainWindow {
+DataContext MainViewModel
+InitializeComponent()
+OnLoaded()
}
class AudioCaptureService {
+StartCapture()
+StopCapture()
+IsCapturing bool
+OnAudioDataReceived()
}
class SessionStore {
+SaveSession(session)
+LoadSession(id)
+DeleteSession(id)
+GetAllSessions()
}
MainViewModel --> AudioCaptureService : "uses"
MainViewModel --> SessionStore : "uses"
MainWindow --> MainViewModel : "binds to"
ClipItemViewModel --> AudioClip : "wraps"
```

**Diagram sources**
- [MainViewModel.cs](file://ViewModels/MainViewModel.cs)
- [ClipItemViewModel.cs](file://ViewModels/ClipItemViewModel.cs)
- [MainWindow.xaml.cs](file://MainWindow.xaml.cs)
- [AudioCaptureService.cs](file://Services/AudioCaptureService.cs)
- [SessionStore.cs](file://Services/SessionStore.cs)

### Service Layer Architecture
The service layer provides specialized functionality for different aspects of the application:

```mermaid
classDiagram
class AudioCaptureService {
-AudioDevice device
-Stream outputStream
+StartCapture() void
+StopCapture() void
+IsCapturing bool
+OnAudioDataReceived byte[]
+ConfigureDevice(deviceId) void
}
class AudioExportService {
-ExportFormat format
-QualitySettings quality
+ExportToWAV(audioData, outputPath) string
+ExportToMP3(audioData, outputPath) string
+ExportToFLAC(audioData, outputPath) string
+SetQuality(quality) void
}
class HotkeyService {
-Dictionary~string,Action~ hotkeyMap
+RegisterHotkey(key, action) void
+UnregisterHotkey(key) void
+OnHotkeyPressed key
+Initialize() void
}
class SessionStore {
-string storagePath
+SaveSession(session) bool
+LoadSession(id) RecordingSession
+DeleteSession(id) bool
+GetAllSessions() RecordingSession[]
+UpdateSession(session) bool
}
class SettingsService {
-AppSettings settings
+GetSetting(key) object
+SetSetting(key, value) void
+SaveSettings() void
+LoadSettings() void
+ResetToDefaults() void
}
class WaveformDataService {
-ProcessingQueue queue
+GenerateWaveformData(audioData) double[]
+AnalyzeFrequency(audioData) FrequencyData
+CalculateRMS(audioData) double
+BatchProcess(dataList) ProcessedData[]
}
AudioCaptureService --> SessionStore : "saves data"
AudioExportService --> SessionStore : "loads clips"
HotkeyService --> MainViewModel : "triggers actions"
WaveformDataService --> AudioCaptureService : "processes data"
```

**Diagram sources**
- [AudioCaptureService.cs](file://Services/AudioCaptureService.cs)
- [AudioExportService.cs](file://Services/AudioExportService.cs)
- [HotkeyService.cs](file://Services/HotkeyService.cs)
- [SessionStore.cs](file://Services/SessionStore.cs)
- [SettingsService.cs](file://Services/SettingsService.cs)
- [WaveformDataService.cs](file://Services/WaveformDataService.cs)

### Data Flow and Event-Driven Communication
The application uses event-driven communication patterns to handle asynchronous operations and real-time updates:

```mermaid
sequenceDiagram
participant User as "User"
participant MainWindow as "MainWindow"
participant MainViewModel as "MainViewModel"
participant AudioCaptureService as "AudioCaptureService"
participant SessionStore as "SessionStore"
participant WaveformDataService as "WaveformDataService"
User->>MainWindow : Click Start Recording
MainWindow->>MainViewModel : StartRecording()
MainViewModel->>AudioCaptureService : StartCapture()
AudioCaptureService-->>MainViewModel : OnAudioDataReceived
MainViewModel->>WaveformDataService : GenerateWaveformData()
WaveformDataService-->>MainViewModel : WaveformData
MainViewModel->>SessionStore : SaveSession()
SessionStore-->>MainViewModel : Success
MainViewModel-->>MainWindow : Update UI State
MainWindow-->>User : Show Recording Indicator
```

**Diagram sources**
- [MainWindow.xaml.cs](file://MainWindow.xaml.cs)
- [MainViewModel.cs](file://ViewModels/MainViewModel.cs)
- [AudioCaptureService.cs](file://Services/AudioCaptureService.cs)
- [SessionStore.cs](file://Services/SessionStore.cs)
- [WaveformDataService.cs](file://Services/WaveformDataService.cs)

### Dependency Injection Patterns
The application implements dependency injection to manage service lifetimes and improve testability:

```mermaid
flowchart TD
AppStartup["Application Startup"] --> DIContainer["Dependency Injection Container"]
DIContainer --> RegisterServices["Register Services"]
RegisterServices --> AudioCaptureService["AudioCaptureService<br/>Singleton"]
RegisterServices --> SessionStore["SessionStore<br/>Singleton"]
RegisterServices --> SettingsService["SettingsService<br/>Singleton"]
RegisterServices --> AudioExportService["AudioExportService<br/>Transient"]
RegisterServices --> HotkeyService["HotkeyService<br/>Singleton"]
RegisterServices --> WaveformDataService["WaveformDataService<br/>Scoped"]
DIContainer --> ViewModelFactory["ViewModel Factory"]
ViewModelFactory --> MainViewModel["MainViewModel<br/>with injected services"]
ViewModelFactory --> ClipItemViewModel["ClipItemViewModel<br/>with injected services"]
MainViewModel --> UseServices["Use Injected Services"]
ClipItemViewModel --> UseServices
```

**Diagram sources**
- [App.xaml.cs](file://App.xaml.cs)
- [MainViewModel.cs](file://ViewModels/MainViewModel.cs)
- [ClipItemViewModel.cs](file://ViewModels/ClipItemViewModel.cs)
- [AudioCaptureService.cs](file://Services/AudioCaptureService.cs)
- [SessionStore.cs](file://Services/SessionStore.cs)
- [SettingsService.cs](file://Services/SettingsService.cs)
- [AudioExportService.cs](file://Services/AudioExportService.cs)
- [HotkeyService.cs](file://Services/HotkeyService.cs)
- [WaveformDataService.cs](file://Services/WaveformDataService.cs)

### State Management Strategies
The application manages state through multiple strategies:

```mermaid
stateDiagram-v2
[*] --> Idle
Idle --> Capturing : "Start Recording"
Capturing --> Paused : "Pause"
Paused --> Capturing : "Resume"
Capturing --> Saving : "Stop Recording"
Paused --> Saving : "Stop Recording"
Saving --> Processing : "Save to Storage"
Processing --> Idle : "Complete"
Saving --> Error : "Save Failed"
Processing --> Error : "Process Failed"
Error --> Idle : "Retry or Cancel"
note right of Capturing : "Real-time audio capture<br/>and waveform generation"
note right of Saving : "Session persistence<br/>and metadata update"
note right of Processing : "Background processing<br/>and validation"
```

**Diagram sources**
- [MainViewModel.cs](file://ViewModels/MainViewModel.cs)
- [AudioCaptureService.cs](file://Services/AudioCaptureService.cs)
- [SessionStore.cs](file://Services/SessionStore.cs)

**Section sources**
- [MainViewModel.cs](file://ViewModels/MainViewModel.cs)
- [ClipItemViewModel.cs](file://ViewModels/ClipItemViewModel.cs)
- [AudioCaptureService.cs](file://Services/AudioCaptureService.cs)
- [AudioExportService.cs](file://Services/AudioExportService.cs)
- [HotkeyService.cs](file://Services/HotkeyService.cs)
- [SessionStore.cs](file://Services/SessionStore.cs)
- [SettingsService.cs](file://Services/SettingsService.cs)
- [WaveformDataService.cs](file://Services/WaveformDataService.cs)

## Dependency Analysis
The application demonstrates clear dependency relationships between components:

```mermaid
graph LR
subgraph "UI Dependencies"
MainWindow --> MainViewModel
WaveformControl --> ClipItemViewModel
end
subgraph "ViewModel Dependencies"
MainViewModel --> AudioCaptureService
MainViewModel --> AudioExportService
MainViewModel --> SessionStore
MainViewModel --> SettingsService
MainViewModel --> WaveformDataService
MainViewModel --> HotkeyService
MainViewModel --> ClipItemViewModel
end
subgraph "Service Dependencies"
AudioCaptureService --> SessionStore
AudioExportService --> SessionStore
WaveformDataService --> AudioCaptureService
end
subgraph "Model Dependencies"
SessionStore --> RecordingSession
SessionStore --> AudioClip
SessionStore --> Marker
SettingsService --> AppSettings
end
```

**Diagram sources**
- [MainWindow.xaml.cs](file://MainWindow.xaml.cs)
- [MainViewModel.cs](file://ViewModels/MainViewModel.cs)
- [ClipItemViewModel.cs](file://ViewModels/ClipItemViewModel.cs)
- [AudioCaptureService.cs](file://Services/AudioCaptureService.cs)
- [AudioExportService.cs](file://Services/AudioExportService.cs)
- [SessionStore.cs](file://Services/SessionStore.cs)
- [SettingsService.cs](file://Services/SettingsService.cs)
- [WaveformDataService.cs](file://Services/WaveformDataService.cs)
- [RecordingSession.cs](file://Models/RecordingSession.cs)
- [AudioClip.cs](file://Models/AudioClip.cs)
- [Marker.cs](file://Models/Marker.cs)
- [AppSettings.cs](file://Models/AppSettings.cs)

**Section sources**
- [MainViewModel.cs](file://ViewModels/MainViewModel.cs)
- [ClipItemViewModel.cs](file://ViewModels/ClipItemViewModel.cs)
- [AudioCaptureService.cs](file://Services/AudioCaptureService.cs)
- [SessionStore.cs](file://Services/SessionStore.cs)

## Performance Considerations
The application implements several performance optimization strategies:

### Asynchronous Operations
- Audio capture operations run on background threads to prevent UI blocking
- Waveform data generation uses asynchronous processing
- File I/O operations are performed asynchronously

### Memory Management
- Efficient audio buffer management to prevent memory leaks
- Proper disposal of audio resources
- Garbage collection optimization for large audio datasets

### Caching Strategies
- Settings caching to reduce disk I/O
- Waveform data caching for improved responsiveness
- Session metadata caching for faster UI updates

### Threading Model
- UI thread for user interactions
- Background threads for audio processing
- Thread-safe data access patterns

## Troubleshooting Guide
Common issues and their solutions:

### Audio Capture Issues
- **Problem**: No audio input detected
  - **Solution**: Check audio device permissions and default device selection
- **Problem**: Audio capture stops unexpectedly
  - **Solution**: Verify audio device availability and check for resource conflicts

### Session Management Problems
- **Problem**: Sessions not saving properly
  - **Solution**: Check storage permissions and available disk space
- **Problem**: Corrupted session data
  - **Solution**: Implement session validation and recovery mechanisms

### Performance Issues
- **Problem**: UI lag during recording
  - **Solution**: Optimize waveform generation and consider reducing sample rate
- **Problem**: High memory usage
  - **Solution**: Implement proper cleanup and garbage collection triggers

### Configuration Issues
- **Problem**: Settings not persisting
  - **Solution**: Verify file path permissions and serialization settings
- **Problem**: Hotkeys not working
  - **Solution**: Check for conflicting global hotkeys and verify registration

**Section sources**
- [AudioCaptureService.cs](file://Services/AudioCaptureService.cs)
- [SessionStore.cs](file://Services/SessionStore.cs)
- [SettingsService.cs](file://Services/SettingsService.cs)
- [HotkeyService.cs](file://Services/HotkeyService.cs)

## Conclusion
The SamplerRecorder application demonstrates a well-architected WPF application using the MVVM pattern with clear separation of concerns. The service layer abstraction provides excellent modularity and testability, while the event-driven communication ensures responsive user interactions. The dependency injection implementation promotes loose coupling and facilitates unit testing.

Key architectural strengths include:
- Clean separation between UI, business logic, and data access
- Comprehensive service layer abstraction
- Event-driven communication patterns
- Proper dependency injection implementation
- Scalable and maintainable code structure

The application's design supports future extensibility through well-defined interfaces and modular service components. The use of standard WPF patterns ensures compatibility with existing development workflows and tooling.