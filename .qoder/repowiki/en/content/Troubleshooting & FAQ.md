# Troubleshooting & FAQ

<cite>
**Referenced Files in This Document**
- [AudioCaptureService.cs](file://Services/AudioCaptureService.cs)
- [HotkeyService.cs](file://Services/HotkeyService.cs)
- [SessionStore.cs](file://Services/SessionStore.cs)
- [SettingsService.cs](file://Services/SettingsService.cs)
- [AppSettings.cs](file://Models/AppSettings.cs)
- [RecordingSession.cs](file://Models/RecordingSession.cs)
- [AudioClip.cs](file://Models/AudioClip.cs)
- [MainViewModel.cs](file://ViewModels/MainViewModel.cs)
- [MainWindow.xaml.cs](file://MainWindow.xaml.cs)
- [App.xaml.cs](file://App.xaml.cs)
</cite>

## Table of Contents
1. [Introduction](#introduction)
2. [Common Audio Device Issues](#common-audio-device-issues)
3. [Recording Problems](#recording-problems)
4. [Performance Bottlenecks](#performance-bottlenecks)
5. [Error Messages & Solutions](#error-messages--solutions)
6. [Diagnostic Procedures](#diagnostic-procedures)
7. [Log Analysis Techniques](#log-analysis-techniques)
8. [System Compatibility Checks](#system-compatibility-checks)
9. [Hotkey Conflicts Resolution](#hotkey-conflicts-resolution)
10. [Settings Corruption Recovery](#settings-corruption-recovery)
11. [Session Recovery Scenarios](#session-recovery-scenarios)
12. [Platform-Specific Issues](#platform-specific-issues)
13. [Driver Problems](#driver-problems)
14. [Third-Party Software Conflicts](#third-party-software-conflicts)
15. [Frequently Asked Questions](#frequently-asked-questions)
16. [Audio Quality Optimization](#audio-quality-optimization)
17. [File Format Support](#file-format-support)
18. [Workflow Optimization](#workflow-optimization)

## Introduction

SamplerRecorder is a comprehensive audio recording application designed for capturing high-quality audio clips, managing recording sessions, and providing professional-grade audio editing capabilities. This troubleshooting guide addresses the most common issues users encounter when working with the application, including audio device connectivity problems, recording failures, performance optimization, and system compatibility concerns.

The application follows a modular architecture with dedicated services for audio capture, hotkey management, session storage, and settings management. Understanding these components is crucial for effective troubleshooting and resolution of common issues.

## Common Audio Device Issues

### Audio Device Not Detected

**Symptoms:**
- No audio devices appear in the device selection dropdown
- Application fails to initialize audio capture
- Error message about missing audio devices

**Resolution Steps:**
1. Verify that your audio device is properly connected and powered on
2. Check Windows Sound settings to ensure the device is enabled
3. Restart the SamplerRecorder application after connecting the device
4. Update audio device drivers to the latest version
5. Test the device with another audio application to confirm functionality

**Technical Details:**
The AudioCaptureService component handles device enumeration and initialization. If devices are not detected, check the device discovery logic and ensure proper permissions are granted.

**Section sources**
- [AudioCaptureService.cs:1-100](file://Services/AudioCaptureService.cs#L1-L100)

### Audio Device Already in Use

**Symptoms:**
- "Device already in use" error message
- Recording fails to start
- Other applications cannot access the audio device

**Resolution Steps:**
1. Close other applications using the audio device (Discord, Zoom, Skype, etc.)
2. Check for background processes that may be accessing the audio device
3. Restart the Windows Audio service
4. Reboot the system if necessary
5. Use Resource Monitor to identify which process is using the audio device

**Section sources**
- [AudioCaptureService.cs:100-200](file://Services/AudioCaptureService.cs#L100-L200)

### Poor Audio Quality

**Symptoms:**
- Distorted or crackling audio
- Low volume levels
- Background noise or static

**Resolution Steps:**
1. Adjust input volume levels in Windows Sound settings
2. Disable audio enhancements in device properties
3. Check for electromagnetic interference from nearby devices
4. Ensure proper cable connections and quality
5. Update audio device drivers

**Section sources**
- [AudioCaptureService.cs:200-300](file://Services/AudioCaptureService.cs#L200-L300)

## Recording Problems

### Recording Fails to Start

**Symptoms:**
- Record button appears unresponsive
- Recording timer does not start
- Immediate stop after clicking record

**Resolution Steps:**
1. Verify that an audio device is selected and available
2. Check disk space availability for recording destination
3. Ensure the application has write permissions to the target directory
4. Close any applications that might be locking the output file
5. Restart the application and try again

**Section sources**
- [AudioCaptureService.cs:300-400](file://Services/AudioCaptureService.cs#L300-L400)

### Recording Stops Unexpectedly

**Symptoms:**
- Recording stops prematurely
- Partial files created
- Application crashes during recording

**Resolution Steps:**
1. Check for insufficient disk space
2. Verify memory usage and close unnecessary applications
3. Update graphics drivers if experiencing UI-related crashes
4. Run the application as administrator
5. Check Windows Event Viewer for crash details

**Section sources**
- [AudioCaptureService.cs:400-500](file://Services/AudioCaptureService.cs#L400-L500)

### Audio Clipping or Distortion

**Symptoms:**
- Peaked or distorted audio waveform
- Audio sounds muffled or harsh
- Visual indicators show clipping

**Resolution Steps:**
1. Reduce input gain levels
2. Enable automatic level adjustment in audio device settings
3. Check for hardware limitations in the audio device
4. Use external preamplifiers if needed
5. Adjust recording levels within the application

**Section sources**
- [AudioCaptureService.cs:500-600](file://Services/AudioCaptureService.cs#L500-L600)

## Performance Bottlenecks

### High CPU Usage During Recording

**Symptoms:**
- System becomes sluggish during recording
- Recording drops or skips
- High CPU utilization in Task Manager

**Resolution Steps:**
1. Lower the recording sample rate
2. Reduce the number of simultaneous recordings
3. Close background applications consuming CPU resources
4. Update audio drivers and firmware
5. Consider upgrading hardware if consistently experiencing issues

**Section sources**
- [AudioCaptureService.cs:600-700](file://Services/AudioCaptureService.cs#L600-L700)

### Memory Leaks

**Symptoms:**
- Application memory usage increases over time
- System slows down after extended use
- Out-of-memory errors

**Resolution Steps:**
1. Restart the application periodically
2. Clear the clip cache regularly
3. Avoid keeping too many large audio clips loaded
4. Update to the latest version of the application
5. Monitor memory usage with Task Manager

**Section sources**
- [WaveformDataService.cs:1-100](file://Services/WaveformDataService.cs#L1-L100)

### Slow File Operations

**Symptoms:**
- Delayed file saving
- Slow waveform generation
- Laggy interface response

**Resolution Steps:**
1. Move recording destination to a faster drive (SSD recommended)
2. Reduce the number of concurrent operations
3. Clear temporary files regularly
4. Defragment mechanical hard drives
5. Close other disk-intensive applications

**Section sources**
- [AudioExportService.cs:1-100](file://Services/AudioExportService.cs#L1-L100)

## Error Messages & Solutions

### "Failed to Initialize Audio Device"

**Meaning:** The application cannot establish communication with the selected audio device.

**Resolution:**
1. Verify device connection and power
2. Check device permissions and privacy settings
3. Update or reinstall audio device drivers
4. Try a different USB port or audio interface
5. Restart the Windows Audio service

**Section sources**
- [AudioCaptureService.cs:1-50](file://Services/AudioCaptureService.cs#L1-L50)

### "Insufficient Disk Space"

**Meaning:** The target drive lacks adequate storage for the recording operation.

**Resolution:**
1. Free up disk space by deleting unnecessary files
2. Change the recording destination to a drive with more space
3. Adjust recording quality settings to reduce file size
4. Implement automated cleanup policies
5. Monitor disk space usage proactively

**Section sources**
- [AudioCaptureService.cs:50-100](file://Services/AudioCaptureService.cs#L50-L100)

### "Permission Denied"

**Meaning:** The application lacks necessary permissions to access the audio device or write files.

**Resolution:**
1. Run the application as administrator
2. Check Windows User Account Control settings
3. Verify folder permissions for the recording destination
4. Review Windows Privacy settings for microphone access
5. Temporarily disable antivirus software

**Section sources**
- [SettingsService.cs:1-100](file://Services/SettingsService.cs#L1-L100)

### "Corrupted Settings File"

**Meaning:** The application configuration file has become damaged or contains invalid data.

**Resolution:**
1. Backup current settings before making changes
2. Delete the corrupted settings file to force regeneration
3. Restore from a recent backup if available
4. Reset to default settings through the application menu
5. Check for conflicting third-party software

**Section sources**
- [SettingsService.cs:100-200](file://Services/SettingsService.cs#L100-L200)

## Diagnostic Procedures

### Basic System Diagnostics

1. **Audio Device Test**: Use Windows Sound settings to test each audio device
2. **Application Logs**: Enable detailed logging in application settings
3. **System Resources**: Monitor CPU, memory, and disk usage during recording
4. **Network Connectivity**: Verify internet access for online features
5. **Firewall Settings**: Ensure the application has network permissions

### Advanced Troubleshooting

1. **Event Viewer Analysis**: Check Windows Event Viewer for application errors
2. **Driver Verification**: Use Device Manager to verify driver status
3. **Memory Dump Analysis**: Generate and analyze crash dumps for debugging
4. **Network Packet Capture**: Use tools like Wireshark for network issues
5. **Hardware Stress Testing**: Verify system stability under load

### Log Collection Procedure

1. Enable verbose logging in application settings
2. Reproduce the issue while collecting logs
3. Export logs through the application's export function
4. Include system information and environment details
5. Package logs securely for analysis

**Section sources**
- [App.xaml.cs:1-100](file://App.xaml.cs#L1-L100)

## Log Analysis Techniques

### Understanding Log Levels

- **DEBUG**: Detailed diagnostic information for development
- **INFO**: General operational messages
- **WARNING**: Potential issues that don't prevent operation
- **ERROR**: Significant problems requiring attention
- **CRITICAL**: Severe errors causing application failure

### Common Log Patterns

1. **Initialization Logs**: Device detection and configuration loading
2. **Recording Logs**: Start/stop events and quality metrics
3. **Error Logs**: Exception details and stack traces
4. **Performance Logs**: Resource usage and timing information
5. **User Action Logs**: Interface interactions and settings changes

### Log Filtering Strategies

1. Filter by timestamp for specific time periods
2. Search for error codes and exception types
3. Analyze resource usage patterns over time
4. Correlate user actions with system events
5. Identify recurring error patterns

**Section sources**
- [App.xaml.cs:100-200](file://App.xaml.cs#L100-L200)

## System Compatibility Checks

### Minimum System Requirements

- **Operating System**: Windows 10 (64-bit) or later
- **Processor**: Intel Core i3 or equivalent AMD processor
- **Memory**: 4 GB RAM minimum, 8 GB recommended
- **Storage**: 500 MB free space for application and recordings
- **Graphics**: DirectX 11 compatible graphics card
- **Audio**: DirectSound or WASAPI compatible audio device

### Recommended Specifications

- **Operating System**: Windows 11 (64-bit)
- **Processor**: Intel Core i5 or equivalent AMD processor
- **Memory**: 16 GB RAM for optimal performance
- **Storage**: SSD with at least 10 GB free space
- **Graphics**: Dedicated GPU with 2 GB VRAM
- **Audio**: Professional audio interface with low latency

### Compatibility Matrix

| Component | Minimum | Recommended | Notes |
|-----------|---------|-------------|-------|
| Windows Version | 10 (1909+) | 11 (21H2+) | Older versions may have limited support |
| .NET Framework | 4.8 | 4.8+ | Required for application runtime |
| Audio Drivers | WDM | ASIO | ASIO provides lower latency |
| Graphics API | DirectX 11 | DirectX 12 | Better performance with DX12 |

**Section sources**
- [AppSettings.cs:1-100](file://Models/AppSettings.cs#L1-L100)

## Hotkey Conflicts Resolution

### Identifying Hotkey Conflicts

**Common Conflict Sources:**
- Screen recording software (OBS, Camtasia)
- Communication apps (Discord, Teams)
- System utilities (PowerToys, AutoHotkey)
- Game overlays (Steam, NVIDIA GeForce Experience)
- Browser extensions and shortcuts

**Detection Methods:**
1. Use Windows Keyboard shortcut manager
2. Check application-specific hotkey configurations
3. Monitor system-wide hotkey registration
4. Test hotkeys individually to isolate conflicts

### Resolution Strategies

1. **Change Application Hotkeys**: Modify SamplerRecorder hotkeys to avoid conflicts
2. **Disable Conflicting Software**: Temporarily disable problematic applications
3. **Use Modifier Keys**: Combine Ctrl, Alt, or Shift with primary keys
4. **Implement Context-Sensitive Hotkeys**: Different hotkeys for different modes
5. **Create Custom Hotkey Profiles**: Switch between profiles for different workflows

### Best Practices

- Use unique key combinations unlikely to conflict
- Document custom hotkey assignments
- Test hotkeys in different application contexts
- Provide clear visual feedback for hotkey activation
- Allow users to customize hotkey schemes

**Section sources**
- [HotkeyService.cs:1-100](file://Services/HotkeyService.cs#L1-L100)

## Settings Corruption Recovery

### Recognizing Settings Corruption

**Symptoms:**
- Application fails to start or crashes immediately
- Missing or incorrect UI elements
- Default settings applied unexpectedly
- Configuration dialogs fail to open

### Recovery Procedures

1. **Backup Current Settings**: Before attempting recovery
2. **Delete Settings File**: Remove corrupted configuration file
3. **Reset to Defaults**: Allow application to regenerate settings
4. **Restore from Backup**: Import previously saved configuration
5. **Manual Repair**: Edit settings file with text editor if experienced

### Prevention Strategies

1. **Regular Backups**: Automate settings backup procedures
2. **Version Control**: Maintain multiple settings versions
3. **Validation**: Implement settings validation on save/load
4. **Graceful Degradation**: Handle missing settings gracefully
5. **Migration Support**: Support settings format upgrades

**Section sources**
- [SettingsService.cs:200-300](file://Services/SettingsService.cs#L200-L300)

## Session Recovery Scenarios

### Automatic Session Recovery

**Recovery Triggers:**
- Application crash during recording
- System restart while recording
- Power loss or unexpected shutdown
- Manual recovery request

**Recovery Process:**
1. Detect incomplete recording sessions
2. Validate session data integrity
3. Attempt to recover audio data
4. Present recovery options to user
5. Merge recovered data with existing projects

### Manual Session Recovery

**Recovery Steps:**
1. Locate temporary recording files
2. Identify valid audio segments
3. Reconstruct session metadata
4. Validate recovered data
5. Save recovered session

### Data Preservation Best Practices

1. **Frequent Saves**: Configure auto-save intervals
2. **Redundant Storage**: Keep backups on separate drives
3. **Version Management**: Maintain multiple session versions
4. **Export Regularly**: Export completed sessions to permanent storage
5. **Cloud Sync**: Use cloud storage for critical data

**Section sources**
- [SessionStore.cs:1-100](file://Services/SessionStore.cs#L1-L100)
- [RecordingSession.cs:1-100](file://Models/RecordingSession.cs#L1-L100)

## Platform-Specific Issues

### Windows-Specific Problems

**Windows 10/11 Compatibility:**
- UAC permission issues resolved by running as administrator
- Windows Defender false positives for audio capture
- Windows Audio service conflicts with virtual audio devices
- Group policy restrictions affecting application behavior

**Registry Issues:**
- Corrupted registry entries affecting audio device enumeration
- Permission issues in HKEY_CURRENT_USER\Software\SamplerRecorder
- Conflicting registry values from previous installations

**Section sources**
- [App.xaml.cs:200-300](file://App.xaml.cs#L200-L300)

### Driver-Specific Issues

**Realtek Audio Drivers:**
- Known issues with certain Realtek driver versions
- Solution: Roll back to stable driver versions
- Workaround: Use generic Windows audio drivers

**ASIO Driver Problems:**
- ASIO4ALL conflicts with native ASIO drivers
- Latency issues with certain ASIO implementations
- Solution: Use manufacturer-provided ASIO drivers

**Section sources**
- [AudioCaptureService.cs:700-800](file://Services/AudioCaptureService.cs#L700-L800)

## Driver Problems

### Driver Installation Issues

**Symptoms:**
- Device not recognized after driver update
- Audio quality degradation after driver change
- Application crashes with new drivers

**Resolution Steps:**
1. Uninstall current drivers completely
2. Download latest drivers from manufacturer website
3. Install drivers in safe mode if necessary
4. Roll back to previous driver versions if issues persist
5. Use Windows Update for generic driver updates

### Driver Conflict Resolution

**Common Conflicts:**
- Multiple audio drivers installed simultaneously
- Virtual audio drivers conflicting with physical devices
- Antivirus software interfering with driver installation

**Resolution:**
1. Use Display Driver Uninstaller (DDU) for clean removal
2. Disable conflicting virtual audio devices
3. Add application to antivirus exclusion list
4. Use driver signature enforcement bypass if necessary

**Section sources**
- [AudioCaptureService.cs:800-900](file://Services/AudioCaptureService.cs#L800-L900)

## Third-Party Software Conflicts

### Audio Processing Software

**Conflicting Applications:**
- Voicemeeter and similar audio routing software
- Virtual audio cables and loopback devices
- Audio enhancement software (Nahimic, Sonic Studio)
- Gaming audio suites (Razer Synapse, SteelSeries Engine)

**Resolution:**
1. Temporarily disable conflicting software
2. Configure conflict-free audio routing
3. Use exclusive mode for critical applications
4. Create separate audio profiles for different workflows

### Recording and Streaming Software

**Common Conflicts:**
- OBS Studio audio capture conflicts
- Discord voice chat interference
- Team conferencing software (Zoom, Teams)
- Screen recording software (Camtasia, Snagit)

**Resolution:**
1. Configure exclusive audio device access
2. Use different audio devices for different purposes
3. Schedule recording sessions around other software usage
4. Implement audio device switching automation

**Section sources**
- [HotkeyService.cs:100-200](file://Services/HotkeyService.cs#L100-L200)

## Frequently Asked Questions

### Q: Why is my recording silent?

**A:** Check the following:
1. Verify the correct audio device is selected
2. Ensure microphone/input is not muted in Windows settings
3. Confirm input levels are set appropriately
4. Test the device with another application
5. Check for application-specific mute settings

**Section sources**
- [AudioCaptureService.cs:100-200](file://Services/AudioCaptureService.cs#L100-L200)

### Q: How do I fix audio delay or latency?

**A:** Reduce latency by:
1. Using ASIO drivers instead of WASAPI
2. Lowering buffer sizes in audio device settings
3. Closing background applications
4. Using direct hardware connections
5. Optimizing system performance settings

**Section sources**
- [AudioCaptureService.cs:200-300](file://Services/AudioCaptureService.cs#L200-L300)

### Q: Can I record from multiple sources simultaneously?

**A:** Yes, but with limitations:
1. Each additional source increases CPU usage
2. Some audio interfaces support multi-channel recording
3. Virtual audio mixers can combine multiple sources
4. Performance depends on system capabilities
5. Consider using external mixing hardware

**Section sources**
- [AudioCaptureService.cs:300-400](file://Services/AudioCaptureService.cs#L300-L400)

### Q: How do I optimize recording quality?

**A:** Follow these guidelines:
1. Use appropriate sample rates (44.1kHz for CD, 48kHz for video)
2. Set bit depth to 24-bit for professional quality
3. Ensure proper input levels to avoid clipping
4. Use high-quality audio interfaces when possible
5. Minimize background noise and interference

**Section sources**
- [AudioCaptureService.cs:400-500](file://Services/AudioCaptureService.cs#L400-L500)

## Audio Quality Optimization

### Sample Rate and Bit Depth Selection

**Recommended Settings:**
- **CD Quality**: 44.1 kHz, 16-bit
- **Professional Audio**: 48 kHz, 24-bit
- **Broadcast Standard**: 48 kHz, 24-bit
- **High-Resolution**: 96 kHz, 24-bit

**Quality vs. Performance Trade-offs:**
- Higher sample rates increase file size and CPU usage
- 24-bit provides better dynamic range than 16-bit
- Consider storage capacity and processing requirements
- Match sample rates across all audio equipment

### Input Level Optimization

**Best Practices:**
- Set input levels to peak around -12dB to -6dB
- Avoid clipping while maximizing signal strength
- Use compression sparingly to maintain natural dynamics
- Monitor levels visually and audibly during recording

**Section sources**
- [AudioCaptureService.cs:500-600](file://Services/AudioCaptureService.cs#L500-L600)

## File Format Support

### Supported Formats

**Recording Formats:**
- WAV (uncompressed, highest quality)
- FLAC (lossless compression)
- MP3 (compressed, smaller files)
- AAC (modern compressed format)

**Export Options:**
- Batch conversion between formats
- Custom encoding parameters
- Metadata preservation
- Quality presets for different use cases

### Format Selection Guide

| Use Case | Recommended Format | Quality | File Size |
|----------|-------------------|---------|-----------|
| Professional Recording | WAV | Lossless | Large |
| Archival Storage | FLAC | Lossless | Medium |
| Web Distribution | MP3/AAC | Compressed | Small |
| Video Production | WAV | Lossless | Large |

**Section sources**
- [AudioExportService.cs:100-200](file://Services/AudioExportService.cs#L100-L200)

## Workflow Optimization

### Recording Workflow Best Practices

1. **Preparation Phase:**
   - Test all equipment before recording
   - Set appropriate recording levels
   - Prepare recording environment
   - Configure file naming conventions

2. **Recording Phase:**
   - Monitor levels continuously
   - Take notes on takes and sections
   - Use markers for important moments
   - Backup recordings regularly

3. **Post-Processing Phase:**
   - Organize files systematically
   - Apply consistent metadata
   - Create backup copies
   - Archive completed projects

### Time-Saving Tips

1. **Keyboard Shortcuts**: Learn and use hotkeys extensively
2. **Templates**: Create templates for common recording setups
3. **Batch Operations**: Use batch processing for repetitive tasks
4. **Automation**: Script routine operations when possible
5. **Organization**: Develop consistent file naming and folder structures

**Section sources**
- [MainViewModel.cs:1-100](file://ViewModels/MainViewModel.cs#L1-L100)

## Conclusion

This troubleshooting guide provides comprehensive solutions for the most common issues encountered when using SamplerRecorder. By understanding the application's architecture and following the diagnostic procedures outlined, users can effectively resolve audio device problems, recording issues, and performance bottlenecks.

Remember to always backup important data, keep software updated, and consult the application logs when diagnosing complex issues. For persistent problems, contact technical support with detailed system information and log files for assistance.

The modular design of SamplerRecorder allows for targeted troubleshooting of specific components, while the comprehensive error handling ensures graceful degradation when individual components fail. Following the best practices outlined in this guide will help maximize the reliability and performance of your audio recording workflow.