# SKERPAPI 開發人員手冊 (Developer Handbook)

## 1. 系統環境要求
*   .NET 8.0 SDK
*   Visual Studio 2022 或 VS Code (搭配 C# 擴充功能)
*   支援 gRPC 的開發環境 (Windows/Linux/macOS)

## 2. 快速開始

### 2.1 複製與建置
```bash
git clone <repository_url>
cd SkiJobControl
dotnet build
```

### 2.2 執行組件
1.  **啟動 Console**:
    ```bash
    cd SkiJobControl.Console
    dotnet run
    ```
    *   Web UI 位址: `http://localhost:5249`
    *   Swagger 文檔: `http://localhost:5249/swagger`

2.  **啟動 Host**:
    ```bash
    cd SkiJobControl.Host
    dotnet run
    ```
    *   Host 會自動連線至 `http://localhost:5250` (gRPC 端點)。

## 3. 專案結構說明
*   `SkiJobControl.Console`: Web 伺服器，包含 SignalR Hubs, gRPC Services, API Controllers。
*   `SkiJobControl.Host`: 背景服務，管理進程池與 gRPC 通訊。
*   `SkiJobControl.Worker`: 範例工作程序，模擬任務執行。
*   `SkiJobControl.Protos`: gRPC `.proto` 定義檔，修改後需重新編譯。
*   `SkiJobControl.Core`: 包含 Repository 模式實作 (如 `FileJobRepository`)。

## 4. 開發指引

### 4.1 修改 gRPC 介面
1.  在 `SkiJobControl.Protos/Protos/job_control.proto` 中新增 `rpc` 或 `message`。
2.  建置專案產出 C# 代碼。
3.  在 `SkiJobControl.Console/Services/JobControlServiceImplementation.cs` 中實作對應邏輯。
4.  在 `SkiJobControl.Host/HostService.cs` 中呼叫新介面。

### 4.2 擴充遠端指令
1.  在 `job_control.proto` 的 `ControlCommand` 訊息中擴充 `oneof payload`。
2.  在 `SkiJobControl.Console/Controllers/ControlController.cs` 中新增 API Endpoint。
3.  在 `SkiJobControl.Host/HostService.cs` 的 `HandleCommand` 方法中處理新指令。

## 5. 測試策略 (TDD)
本專案採用測試驅動開發 (Test-Driven Development)。
*   單元測試應放在對應專案的 `.Tests` 專案中。
*   開發新功能前，應先撰寫失敗的測試案例。
*   確保所有測試通過後再進行重構。

## 6. 常見問題 (FAQ)
*   **gRPC 連線失敗**: 檢查 `appsettings.json` 中的 `ConsoleUrl` 是否正確，並確認埠號 5250 是否被佔用。
*   **Worker 無法啟動**: 確保 `SkiJobControl.Worker.exe` (或 dll) 的路徑設定正確，且具備執行權限。
