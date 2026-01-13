# 同步服務重構後流程說明

## 目標
- 同步流程清晰：**取得遠端水位 → 查本地 CT → 分批 → 送 Apply → 更新水位（由 B 端負責）**。
- 請求格式固定為 `Op/Key/Data`，避免 B 端 400。
- B 端套用資料與更新水位同一交易完成。
- 所有轉型採用文化無關格式。

## A 端（SyncA.Console）流程
1. **取得遠端水位**：`SyncApiClient.GetStateAsync` 讀取 B 端 `ZZ_SYNC_STATE`。 
2. **查本地 CT**：`ChangeTrackingRepository.GetChangesAsync` 取得變更列表。
3. **分批**：`BatchingExtensions.Batch` 依批次大小切割。
4. **建立同步請求**：`SyncRequestBuilder.BuildAsync` 依 `ISyncTableDefinition` 取得欄位、查詢資料並組成 JSON。
5. **送 Apply**：`SyncApiClient.ApplyAsync` 送出每批 JSON。

## B 端（SyncB.Api）流程
1. **驗證版本一致性**：`SyncApplyService` 先讀取 `ZZ_SYNC_STATE`。
2. **驗證資料表**：`SyncTableDefinitionRegistry` 確認 table name 在白名單內。
3. **資料轉型**：`PdfConfigSyncTableDefinition` 以 `SyncValueParser` 轉為強型別資料列（文化無關）。
4. **批次套用**：以 TVP 呼叫 `dbo.usp_PDFConfigSync_Upsert` 與 `dbo.usp_PDFConfigSync_Delete`。
5. **更新水位**：同一交易內更新 `ZZ_SYNC_STATE`。

## 擴充點
- 新增資料表時，只需新增一個 `ISyncTableDefinition` 實作，並註冊至 DI。
- `SyncTableDefinitionRegistry` 會自動提供白名單驗證。

## 交易與一致性
- B 端套用與更新水位在同一 `SqlTransaction` 內進行，任一步驟失敗即 rollback。
- 版本檢查避免重送造成錯誤。
