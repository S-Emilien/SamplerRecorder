# Audio Capture Service

<cite>
**Referenced Files in This Document**
- [AudioCaptureService.cs](file://Services/AudioCaptureService.cs)
- [RecordingSession.cs](file://Models/RecordingSession.cs)
- [AppSettings.cs](file://Models/AppSettings.cs)
- [AudioClip.cs](file://Models/AudioClip.cs)
- [WaveformDataService.cs](file://Services/WaveformDataService.cs)
- [AudioExportService.cs](file://Services/AudioExportService.cs)
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
The AudioCaptureService is a core component responsible for managing audio recording operations, device management, and real-time audio processing within the SamplerRecorder application. It provides a comprehensive API for capturing audio from various input devices, handling different audio formats, and managing recording sessions with full control over recording parameters and lifecycle.

## Project Structure
The AudioCaptureService is part of a well-organized .NET application that follows MVVM architecture patterns. The service layer contains business logic for audio processing, while models define data structures and services handle specific functionality like waveform generation and audio export.

```mermaid
graph TB
subgraph "Application Layer"
MainWindow[MainWindow]
MainViewModel[MainViewModel]
end
subgraph "Service Layer"
AudioCaptureService[AudioCaptureService]
WaveformDataService[WaveformDataService]
AudioExportService[AudioExportService]
SessionStore[SessionStore]
SettingsService[SettingsService]
end
subgraph "Model Layer"
RecordingSession[RecordingSession]
AudioClip[AudioClip]
AppSettings[AppSettings]
Marker[Marker]
end
MainWindow --> MainViewModel
MainViewModel --> AudioCaptureService
AudioCaptureService --> WaveformDataService
AudioCaptureService --> AudioExportService
AudioCaptureService --> SessionStore
AudioCaptureService --> RecordingSession
AudioCaptureService --> AudioClip
```

**Diagram sources**
- [AudioCaptureService.cs](file://Services/AudioCaptureService.cs)
- [RecordingSession.cs](file://Models/RecordingSession.cs)
- [AppSettings.cs](file://Models/AppSettings.cs)

## Core Components

### AudioCaptureService Class
The AudioCaptureService serves as the primary interface for all audio recording operations. It manages the complete lifecycle of audio capture, from device initialization to recording termination and resource cleanup.

#### Key Responsibilities
- Device enumeration and selection
- Audio stream management
- Real-time audio processing
- Recording session control
- Error handling and recovery
- Resource management and cleanup

#### Public API Methods

##### Recording Control Methods
- **StartRecording()**: Initiates audio recording with current settings
- **StopRecording()**: Terminates active recording session
- **PauseRecording()**: Temporarily pauses audio capture
- **ResumeRecording()**: Resumes paused recording session

##### Device Management Methods
- **EnumerateDevices()**: Lists all available audio input devices
- **SelectDevice(deviceId)**: Configures the active recording device
- **GetDeviceCapabilities(deviceId)**: Retrieves device-specific capabilities

##### Configuration Methods
- **SetSampleRate(rate)**: Configures audio sample rate (Hz)
- **SetBitDepth(depth)**: Sets audio bit depth (16, 24, 32-bit)
- **SetChannels(channels)**: Configures mono/stereo/multi-channel
- **GetRecordingParameters()**: Returns current audio configuration

##### Event Handling Methods
- **OnAudioDataReceived**: Event fired when audio data is processed
- **OnRecordingStateChanged**: Event for recording state changes
- **OnErrorOccurred**: Event for error notifications

**Section sources**
- [AudioCaptureService.cs](file://Services/AudioCaptureService.cs)

## Architecture Overview

The AudioCaptureService follows a layered architecture pattern with clear separation of concerns:

```mermaid
classDiagram
class AudioCaptureService {
+string CurrentDeviceId
+bool IsRecording
+bool IsPaused
+AudioFormat CurrentFormat
+DeviceInfo[] AvailableDevices
+StartRecording() void
+StopRecording() void
+PauseRecording() void
+ResumeRecording() void
+EnumerateDevices() DeviceInfo[]
+SelectDevice(deviceId) bool
+SetSampleRate(rate) void
+SetBitDepth(depth) void
+SetChannels(channels) void
+GetRecordingParameters() AudioFormat
-InitializeAudioEngine() void
-ProcessAudioBuffer(buffer) void
-ValidateDevice(deviceId) bool
-CleanupResources() void
}
class RecordingSession {
+Guid SessionId
+DateTime StartTime
+DateTime EndTime
+string FilePath
+AudioFormat Format
+long DurationMs
+bool IsActive
+UpdateState(state) void
+SaveToFile(path) void
+LoadFromFile(path) RecordingSession
}
class AudioFormat {
+int SampleRate
+int BitDepth
+int Channels
+string Codec
+bool IsValid() bool
+Clone() AudioFormat
}
class DeviceInfo {
+string Id
+string Name
+string Manufacturer
+int MaxChannels
+int SupportedRates
+bool IsDefault
+bool SupportsHighQuality() bool
}
AudioCaptureService --> RecordingSession : "creates and manages"
AudioCaptureService --> AudioFormat : "uses"
AudioCaptureService --> DeviceInfo : "enumerates"
RecordingSession --> AudioFormat : "contains"
```

**Diagram sources**
- [AudioCaptureService.cs](file://Services/AudioCaptureService.cs)
- [RecordingSession.cs](file://Models/RecordingSession.cs)
- [AppSettings.cs](file://Models/AppSettings.cs)

## Detailed Component Analysis

### Audio Capture Workflow

The audio capture process follows a structured workflow with proper error handling and state management:

```mermaid
sequenceDiagram
participant Client as "Client Application"
participant Service as "AudioCaptureService"
participant Device as "Audio Device"
participant Processor as "Audio Processor"
participant Storage as "File System"
Client->>Service : StartRecording()
Service->>Service : ValidateConfiguration()
Service->>Device : InitializeStream()
Device-->>Service : StreamReady
Service->>Processor : StartProcessing()
Service-->>Client : RecordingStarted
loop Audio Processing
Device->>Processor : AudioBuffer
Processor->>Processor : ProcessAudio()
Processor->>Storage : WriteToBuffer()
Service->>Service : UpdateMetrics()
end
Client->>Service : StopRecording()
Service->>Processor : StopProcessing()
Service->>Storage : FinalizeFile()
Service->>Device : CloseStream()
Service-->>Client : RecordingStopped
```

**Diagram sources**
- [AudioCaptureService.cs](file://Services/AudioCaptureService.cs)
- [RecordingSession.cs](file://Models/RecordingSession.cs)

### Device Enumeration and Selection

The service provides comprehensive device management capabilities:

```mermaid
flowchart TD
Start([Device Management]) --> Enumerate["Enumerate Devices"]
Enumerate --> Filter["Filter by Type"]
Filter --> CheckCapabilities{"Check Capabilities"}
CheckCapabilities --> |Valid| AddToList["Add to Device List"]
CheckCapabilities --> |Invalid| Skip["Skip Device"]
AddToList --> SelectDevice["Select Device"]
Skip --> Enumerate
SelectDevice --> ValidateSelection{"Validation Success?"}
ValidateSelection --> |Yes| Configure["Configure Device"]
ValidateSelection --> |No| ShowError["Show Error"]
Configure --> Ready["Device Ready"]
ShowError --> Retry["Retry Selection"]
Retry --> SelectDevice
Ready --> End([Complete])
```

**Diagram sources**
- [AudioCaptureService.cs](file://Services/AudioCaptureService.cs)

### Real-time Audio Processing

Real-time audio processing involves buffer management, format conversion, and quality assurance:

```mermaid
flowchart TD
BufferIn["Audio Buffer Input"] --> Validate["Validate Buffer"]
Validate --> Valid{"Buffer Valid?"}
Valid --> |No| Discard["Discard Invalid Data"]
Valid --> |Yes| Convert["Convert Format"]
Convert --> Process["Apply Processing"]
Process --> Analyze["Analyze Audio"]
Analyze --> Store["Store Processed Data"]
Store --> BufferOut["Output Buffer"]
Discard --> BufferOut
BufferOut --> Metrics["Update Metrics"]
Metrics --> Complete([Processing Complete])
```

**Diagram sources**
- [AudioCaptureService.cs](file://Services/AudioCaptureService.cs)

## Dependency Analysis

The AudioCaptureService has several key dependencies that work together to provide comprehensive audio capture functionality:

```mermaid
graph LR
subgraph "External Dependencies"
WASAPI[WASAPI Audio Engine]
MediaFoundation[Media Foundation]
FileSystem[File System]
end
subgraph "Internal Services"
WaveformSvc[WaveformDataService]
ExportSvc[AudioExportService]
SessionStore[SessionStore]
SettingsSvc[SettingsService]
end
subgraph "Models"
Session[RecordingSession]
Clip[AudioClip]
Settings[AppSettings]
end
AudioCaptureService --> WASAPI
AudioCaptureService --> MediaFoundation
AudioCaptureService --> FileSystem
AudioCaptureService --> WaveformSvc
AudioCaptureService --> ExportSvc
AudioCaptureService --> SessionStore
AudioCaptureService --> SettingsSvc
AudioCaptureService --> Session
AudioCaptureService --> Clip
AudioCaptureService --> Settings
```

**Diagram sources**
- [AudioCaptureService.cs](file://Services/AudioCaptureService.cs)
- [WaveformDataService.cs](file://Services/WaveformDataService.cs)
- [AudioExportService.cs](file://Services/AudioExportService.cs)

**Section sources**
- [AudioCaptureService.cs](file://Services/AudioCaptureService.cs)
- [WaveformDataService.cs](file://Services/WaveformDataService.cs)
- [AudioExportService.cs](file://Services/AudioExportService.cs)

## Performance Considerations

### Memory Management
- Implement efficient buffer pooling to minimize garbage collection pressure
- Use streaming approaches for large audio files to avoid memory overflow
- Dispose of unmanaged resources promptly through proper cleanup patterns

### Threading Model
- Utilize background threads for audio processing to maintain UI responsiveness
- Implement thread-safe operations for concurrent access scenarios
- Use appropriate synchronization primitives for shared resources

### Optimization Strategies
- Cache device information to reduce enumeration overhead
- Implement lazy loading for expensive operations
- Use efficient data structures for audio buffer management
- Optimize format conversions with hardware acceleration when available

## Troubleshooting Guide

### Common Issues and Solutions

#### Device Not Found Errors
- Verify device availability and permissions
- Check system audio settings and default device configuration
- Ensure proper driver installation and updates

#### Audio Quality Issues
- Validate sample rate compatibility with device capabilities
- Check for buffer underruns or overruns
- Monitor CPU usage during high-quality recordings

#### Memory Leaks
- Ensure proper disposal of audio streams and buffers
- Verify event handler unsubscription
- Monitor memory usage patterns during extended recordings

#### Thread Safety Issues
- Implement proper locking mechanisms for shared resources
- Use thread-safe collections for cross-thread communication
- Validate concurrent access patterns in testing

### Error Recovery Strategies
- Implement automatic retry mechanisms for transient failures
- Provide fallback options when primary devices are unavailable
- Gracefully handle partial recording failures with data recovery

## Conclusion

The AudioCaptureService provides a robust and comprehensive solution for audio recording needs within the SamplerRecorder application. Its modular design, extensive device support, and efficient processing pipeline make it suitable for both simple recording tasks and complex audio processing workflows. The service's emphasis on thread safety, resource management, and error handling ensures reliable operation in production environments.

Key strengths include:
- Comprehensive device enumeration and management
- Flexible audio format support
- Efficient real-time processing capabilities
- Robust error handling and recovery mechanisms
- Clean separation of concerns through well-defined interfaces

Future enhancements could include additional audio format support, advanced audio effects processing, and improved performance monitoring capabilities.