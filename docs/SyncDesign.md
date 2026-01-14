# SQL Server Change Tracking 同步系統設計文件（跨網域、僅走 HTTP API）

> 本文件與程式碼為「設計文件 + 原始碼草稿」，未宣告可直接編譯或部署。

## 1. 系統目標與假設
- 來源（A）資料庫：`dcmatev4`
- 目標（B）資料庫：`dcmatev4`（表結構相同）
- 同步表：`dbo.PDFConfigSyncServiceConfig`，主鍵 `ID uniqueidentifier`
- A 端已啟用 Change Tracking（DB + Table）
- 同步頻率：每 5 分鐘由排程啟動 A 端 Console 執行一次，非常駐 Service
- 跨網域僅允許 HTTP API 呼叫，無 direct DB 連線

## 2. 架構概覽
- **A 端（Console）**
  - 連線 A DB，使用 Change Tracking 取得變更
  - 呼叫 B 端 API 套用變更
  - 不維護本地水位，統一以 B 端 `ZZ_SYNC_STATE` 為準

- **B 端（ASP.NET Core API）**
  - 提供同步水位查詢與套用 API
  - 使用 Transaction 進行 Upsert + Delete + 更新水位

## 3. 資料流程（每次同步）
1. A 呼叫 `GET /api/sync/pdf-config/state?syncKey=...` 取得 `LastVersion`
2. A 取得 `currentVersion = CHANGE_TRACKING_CURRENT_VERSION()`
3. A 以 `CHANGETABLE(CHANGES ...)` 取得 ID + 操作別（I/U/D）
4. A 對 I/U 的 ID 取得完整列資料（使用 TVP 或批次查詢）
5. A 以批次（例如 2000 筆）呼叫 `POST /api/sync/pdf-config/apply`
6. B 成功後更新 `ZZ_SYNC_STATE.LastVersion`，失敗則不更新

## 4. API 規格
### 4.1 取得同步水位
`GET /api/sync/pdf-config/state?syncKey=...`
- 回傳欄位：`SyncKey`, `LastVersion`, `LastSyncTime`, `LastError`

### 4.2 套用同步
`POST /api/sync/pdf-config/apply`
Body:
```json
{
  "syncKey": "dcmatev4.dbo.PDFConfigSyncServiceConfig",
  "fromVersion": 100,
  "toVersion": 120,
  "upserts": [ { "id": "...", "configKey": "..." } ],
  "deletes": [ "guid1", "guid2" ]
}
```
B 端流程：
- 驗證 `fromVersion == ZZ_SYNC_STATE.LastVersion`
- Transaction：
  1) MERGE Upsert 全欄位
  2) 批次 Delete
  3) 更新 `ZZ_SYNC_STATE`（成功才寫入 `LastVersion=toVersion`, `LastError=NULL`）

## 5. 變更偵測演算法與效能分析
### 5.1 演算法選擇
- 使用 **Change Tracking** 取得變更清單，並以 `ROW_NUMBER()` 取得每筆最新操作，避免同一筆資料多次出現（符合「最後狀態為準」的同步需求）。

### 5.2 時間/空間複雜度
- 取變更清單：`O(N)`
- 批次傳輸與套用：`O(N)`
- 記憶體用量：以批次大小 `B` 為單位，`O(B)`

### 5.3 替代方案比較
| 方案 | 優點 | 缺點 |
|---|---|---|
| Change Tracking | 效能佳、實作簡潔 | 需維護 CT retention、無完整歷史 |
| CDC | 具完整歷史 | 設定成本高、DB 負擔較重 |
| Trigger + Log Table | 可自訂紀錄 | 易受錯誤影響、開發與維護成本高 |

## 6. 可靠性與錯誤防範
- **版本亂序**：B 端驗證 `fromVersion` 必須等於 `LastVersion`，避免亂序或重播
- **CT retention 過期**：A 若偵測版本前進但變更清單為空，會記錄警告，需人工評估是否重建水位
- **批次成功與一致性**：非最後批次使用 `toVersion = fromVersion`，確保只在最後批次成功時才前進水位

## 7. 可維護性與可重用性
- 以 `Repository` + `Service` 分層
- 使用強型別模型與 Options 設定
- 透過 `TableValuedParameterBuilder` 統一 TVP 建構

## 8. 重構建議（若後續需求增加）
- **共用模型與工具庫**：將 A/B 共用模型移至 `Shared` 專案，避免重複
- **批次策略抽象**：將批次策略抽象成介面，便於支援多表同步
- **API 授權**：加入 API Key 或 JWT 以提升安全性

## 9. 驗證與正確性
- B 端在 transaction 內執行 Upsert + Delete + 水位更新，確保一致性
- A 端只在 API 成功回應後才進行下一批，確保不跳過變更

## 10. 對應程式碼位置
- A 端 Console：`src/SyncA.Console`
- B 端 API：`src/SyncB.Api`
- SQL 腳本：`sql/SyncA.sql`, `sql/SyncB.sql`

## 11. 注意事項
- `PdfConfigSyncRow` 欄位為範例，請依實際 `dbo.PDFConfigSyncServiceConfig` 欄位補齊
- TVP 欄位需與實際表結構一致
- 排程建議採用 OS 排程（Windows 工作排程 / Linux Cron）每 5 分鐘執行一次 A 端 Console
