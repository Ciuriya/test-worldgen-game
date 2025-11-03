using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using MyBox;
using UnityEngine;

namespace PendingName.Log {
    public class CustomLogger : MonoBehaviour {
        private struct LogEntry {
            public DateTime Time;
            public LogLevel Level;
            public string Message;
            public string StackTrace;

            public readonly string ToString(bool forUnity) {
                StringBuilder builder = new StringBuilder();

                builder.Append("[CL • ");

                if (!forUnity)
                    builder.Append(Time.ToString("yyyy-MM-dd HH:mm:ss") + " • ");

                builder.Append(Level.ToString() + "] ");
                builder.Append(Message);

                if (!string.IsNullOrEmpty(StackTrace))
                    builder.Append("\n" + StackTrace);

                return builder.ToString();
            }
        }
        
        private struct LogFile {
            public string Name;
            public int Number;
            public long Size;
		}
        
        public static CustomLogger Instance {
            get => _instance;
            set => _instance = value;
        }
        private static CustomLogger _instance;

        [RuntimeInitializeOnLoadMethod]
        private static void ResetInstance() => _instance = null;

        [Header("Settings")]
        [Tooltip("Should logging be enabled?")]
        [SerializeField] private bool _enableLogging = true;

        [Tooltip("The amount of log files stored at once.")]
        [SerializeField] private int _maxLogFiles = 3;

        [Tooltip("The max log file size, in MB.")]
        [SerializeField] private int _maxFileSize = 10;

        [Tooltip("How long should the write thread sleep between loops, in milliseconds.")]
        [SerializeField] private int _writeThreadSleepInterval = 50;

        [Header("Options")]
        [Tooltip("Should the logging options be manually forced to the following options? (auto-selected by default)")]
        [SerializeField] private bool _overrideLoggingOptions = false;

        [Tooltip("Should we log to Unity?")]
        [SerializeField, ConditionalField(nameof(_overrideLoggingOptions))]
        private bool _allowUnityLogging;

        [Tooltip("Should we log to file?")]
        [SerializeField, ConditionalField(nameof(_overrideLoggingOptions))]
        private bool _allowLoggingToFile;

        [Tooltip("Should we allow debug-level logs?")]
        [SerializeField, ConditionalField(nameof(_overrideLoggingOptions))]
        private bool _allowDebugLogging;

        private string _logDirectory;
        private LogFile _currentLogFile;
        private List<LogFile> _existingLogFiles;
        private Thread _writeThread;
        private ConcurrentQueue<LogEntry> _queuedLogs;
        private StreamWriter _logWriter;
        private bool _shouldProcess;
        private long _maxLogSizeAsBytes;

        void Start() {
            if (_instance == null)
                _instance = this;
            else {
                Destroy(this);
                return;
            }

            if (!_enableLogging) return;

            DontDestroyOnLoad(this);

            if (!_overrideLoggingOptions) {
#if DEVELOPMENT_BUILD || UNITY_EDITOR
                _allowUnityLogging = true;
                _allowDebugLogging = true;
                _allowLoggingToFile = false;
#else
                _allowUnityLogging = false;
                _allowDebugLogging = false;
                _allowLoggingToFile = true;
#endif
            }

            Init();
        }

        private void Init() {
            _logDirectory = Path.Combine(Application.persistentDataPath, "Logs");

            if (!Directory.Exists(_logDirectory)) Directory.CreateDirectory(_logDirectory);

            _maxLogSizeAsBytes = _maxFileSize * 1024 * 1024;
            _existingLogFiles = new List<LogFile>();
            _queuedLogs = new ConcurrentQueue<LogEntry>();
            LoadLogs();

            _shouldProcess = true;
            _writeThread = new Thread(WriteThreadLoop) {
                IsBackground = true
            };

            _writeThread.Start();

            Application.logMessageReceivedThreaded += UnityLog;

            Log(LogLevel.Info, "Started Logging\n"
                             + $"\nPlatform: {Application.platform}"
                             + $"\nOS: {SystemInfo.operatingSystem}"
                             + $"\nDevice: {SystemInfo.deviceModel}"
                             + $"\nBuild {Application.version}"
                             + $"\nUnity {Application.unityVersion}");
        }
        
        private void UnityLog(string message, string stackTrace, LogType type) {
            if (message.Length == 0) return;
            if (message.Contains("[CL")) return;

            LogLevel level = LogLevel.Debug;

            if (type == LogType.Log) level = LogLevel.Info;
            else if (type == LogType.Warning) level = LogLevel.Warning;
            else if (type == LogType.Error || type == LogType.Exception) level = LogLevel.Error;

            Log(level, message, level > LogLevel.Info ? stackTrace : "", true);
		}

        public void Log(LogLevel level, string message, string stackTrace = "", bool forceBlockUnityLogs = false) {
            if (!_enableLogging || (!_allowLoggingToFile && !_allowUnityLogging)) return;
            if (!_allowDebugLogging && level == LogLevel.Debug) return;

            LogEntry log = new LogEntry {
                Time = DateTime.Now,
                Level = level,
                Message = message,
                StackTrace = stackTrace
            };

            try {
                if (_allowLoggingToFile) QueueLogToFile(log);
                if (!forceBlockUnityLogs && _allowUnityLogging) LogToUnity(log);
            } catch (Exception e) {
                Debug.LogError($"Failed to log.\n{e.Message}\n{e.StackTrace}");
			} 
        }

        private void LogToUnity(LogEntry log) {
            switch (log.Level) {
                case LogLevel.Debug:
                case LogLevel.Info:
                    Debug.Log(log.ToString(true));
                    break;
                case LogLevel.Warning:
                    Debug.LogWarning(log.ToString(true));
                    break;
                case LogLevel.Error:
                    Debug.LogError(log.ToString(true));
                    break;
                default: break;
            }
        }

        private void QueueLogToFile(LogEntry log) => _queuedLogs.Enqueue(log);
		
        private void WriteThreadLoop() {
            try {
                CleanWriter();
                CreateNewWriter();

                while (_shouldProcess || !_queuedLogs.IsEmpty) {
                    if (_queuedLogs.TryDequeue(out LogEntry log))
                        LogToFile(log);
                    else _logWriter?.Flush();

                    Thread.Sleep(_writeThreadSleepInterval);
                }
            } catch (Exception e) {
                Debug.LogError($"Failed to write to file.\n{e.Message}\n{e.StackTrace}");
            } finally {
                CleanWriter();
            }
		}

        private void LogToFile(LogEntry log) {
            if (_logWriter != null) {
                string logMessage = log.ToString(false);

                _logWriter.WriteLine(logMessage + "\n");
                _currentLogFile.Size += Encoding.UTF8.GetByteCount(logMessage) + 4; // +2 per new line

                if (_currentLogFile.Size >= _maxLogSizeAsBytes)
                    RotateLogs();
            }
        }

        private void LoadLogs() {
            _existingLogFiles.Clear();

            foreach (string logName in Directory.EnumerateFiles(_logDirectory)) {
                string realLogName = logName.Substring(_logDirectory.Length + 1);

                if (realLogName.StartsWith("Log-")) {
                    string logNumberStr = realLogName.Split("-")[1].Split(".")[0];

                    if (int.TryParse(logNumberStr, out int logNumber)) {
                        LogFile file = new LogFile {
                            Name = logName,
                            Number = logNumber,
                            Size = new FileInfo(Path.Combine(_logDirectory, logName)).Length
                        };

                        _existingLogFiles.Add(file);
                    }
                }
            }

            SortLogs();

            bool hasLogFile = true;
            int count = _existingLogFiles.Count;

            if (count == 0) 
                CreateLog(1);
            else if (_existingLogFiles[0].Size < _maxLogSizeAsBytes)
                _currentLogFile = _existingLogFiles[0];
            else hasLogFile = false;

            if (count > _maxLogFiles) {
				for (int i = 0; i < count - _maxLogFiles; ++i) {
                    if (_existingLogFiles.Count <= count - 1 - i) break;

                    RemoveLog(_existingLogFiles[count - 1 - i]);
				}
			} else if (!hasLogFile) RotateLogs();
        }

        private void CreateLog(int number) {
            _currentLogFile = new LogFile {
                Name = "Log-" + number + ".txt",
                Number = number,
                Size = 0
            };

            _existingLogFiles.Add(_currentLogFile);
            SortLogs();

            CleanWriter();
            CreateNewWriter();
        }

        private void RotateLogs() {
            if (_existingLogFiles.Count == _maxLogFiles)
                RemoveLog(_existingLogFiles[_existingLogFiles.Count - 1]);

            CreateLog(_currentLogFile.Number + 1);
        }

        private void RemoveLog(LogFile logFile) {
            _existingLogFiles.Remove(logFile);
            new FileInfo(Path.Combine(_logDirectory, logFile.Name)).Delete();
		}

        private void SortLogs() {
            _existingLogFiles.Sort((a, b) => { return b.Number.CompareTo(a.Number); });
        }

        private void CreateNewWriter() {
			_logWriter = new StreamWriter(Path.Combine(_logDirectory, _currentLogFile.Name), true, Encoding.UTF8) {
                AutoFlush = false
            };
		}

        private void CleanWriter() {
			_logWriter?.Flush();
            _logWriter?.Close();
            _logWriter?.Dispose();
            _logWriter = null;
		}

		private void OnApplicationQuit() => Stop();

		private void OnDestroy() {
            if (_instance == this) Stop();
        }
        
        private void Stop() {
            if (!_shouldProcess) return;

            _shouldProcess = false;
            Application.logMessageReceivedThreaded -= UnityLog;

            Log(LogLevel.Info, "Ending Logging...");

            if (_writeThread != null && _writeThread.IsAlive)
                _writeThread.Join(5000);

            CleanWriter();
		}
	}

    public enum LogLevel {
        Debug = 0,
        Info,
        Warning,
        Error
    }
}