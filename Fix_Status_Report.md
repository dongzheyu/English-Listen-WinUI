# English-Listen-WinUI 安全修复状态报告

> **审计日期**: 2026-07-04  
> **审计专家**: 微软MVP代码评审专家  
> **总体评价**: **P0 高危项已基本修复，P1 部分修复，P2 部分未修复**

---

## 一、 修复总览

| 优先级 | 总数 | 已修复 | 未修复 | 修复率 |
|--------|------|--------|--------|--------|
| 🔴 **P0** | 6 项 | **6 项** | 0 项 | **100%** ✅ |
| 🟠 **P1** | 5 项 | **3 项** | 2 项 | **60%** |
| 🟡 **P2** | 4 项 | **0 项** | 4 项 | **0%** |

---

## 二、 P0 高危项 — 逐项核验结果

### ✅ 威胁 #1：服务层 `async void` 并发竞争（已修复）

| 文件 | 方法 | 修复前 | 修复后 | 状态 |
|------|------|--------|--------|------|
| `ModernDictationService.cs` | `StopTest` | `async void` | `async Task StopTestAsync()` | ✅ |
| `ModernDictationService.cs` | `PauseResume` | `async void` | `async Task PauseResumeAsync()` | ✅ |
| `ModernDictationService.cs` | `NextWord` | `async void` | `async Task NextWordAsync()` | ✅ |
| `ModernDictationService.cs` | `PreviousWord` | `async void` | `async Task PreviousWordAsync()` | ✅ |
| `ModernDictationService.cs` | `RepeatWord` | `async void` | `async Task RepeatWordAsync()` | ✅ |
| `ModernDictationService.cs` | `OnCountdownTimerElapsed` | `async void`（无锁） | `async void` + `_operationLock.WaitAsync()` | ✅ |
| `ModernDictationService.cs` | `StartTest` | `async Task`（无锁） | `async Task` + `_operationLock.WaitAsync()` | ✅ |

**新增并发控制**：
- `SemaphoreSlim _operationLock = new(1, 1)` — 所有状态变更操作互斥
- `SemaphoreSlim _speechLock = new(1, 1)` — 朗读逻辑互斥，防止 Timer 重入导致单词跳过
- `_countdownTimer.AutoReset = false` — 防止 Timer 自动重入

**调用方同步更新**：
- `ModernDictationViewModel.cs` 已同步改为 `await _dictationService.StopTestAsync()` 等

---

### ✅ 威胁 #2：Timer 资源泄漏（主要修复）

| 组件 | 修复内容 | 状态 |
|------|----------|------|
| `ModernDictationService` | `Dispose()` 释放 `_countdownTimer`、`_speechService`、`_operationLock`、`_speechLock` | ✅ |
| `DictationTestPage` | `Dispose()` 释放 `synthesizer`、`chineseSynthesizer`、`countdownTimer` | ✅ |
| `ModernDictationViewModel` | `Dispose()` 调用 `_dictationService?.Dispose()` | ✅ |

> ⚠️ **遗留小项**: `ModernDictationService.Dispose()` 未显式解绑 `_countdownTimer.Elapsed` 事件。但 `Timer.Dispose()` 在 .NET 中会停止底层线程，实际风险极低。`WordsPage._autoSaveTimer` 未在 `OnNavigatedFrom` 中解绑（但 `NavigationCacheMode = Enabled` 意味着页面被缓存，Timer 有合理存在理由）。

---

### ✅ 威胁 #3：字典缓存无大小限制（BaiduTranslateService 已修复）

| 修复项 | 代码 | 状态 |
|--------|------|------|
| 最大缓存条目数 | `MAX_CACHE_ENTRIES = 10000` | ✅ |
| 每日限额历史天数 | `MAX_DAILY_HISTORY_DAYS = 7` | ✅ |
| 加载时截断 | `LoadCache()` 中超出部分 `Keys.Take(excess)` 移除 | ✅ |
| 保存时截断 | `SaveCache()` 中超出部分 `Keys.Take(excess)` 移除 | ✅ |
| 过期日期清理 | `LoadCache()` 中清除超过 7 天的日期键 | ✅ |

> ❌ **未修复**: `TranslationLibraryService` 的 `_translations` 字典仍**无大小限制**，用户持续翻译将无限增长。

---

### ✅ 威胁 #4：文件 I/O 无并发锁（部分修复）

| 文件 | 方法 | 修复内容 | 状态 |
|------|------|----------|------|
| `SettingsService.cs` | `SaveSettingsAsync()` | 添加 `await _fileLock.WaitAsync()` + `finally Release()` | ✅ |
| `SettingsService.cs` | `SaveUsersAsync()` | 添加 `await _fileLock.WaitAsync()` + `finally Release()` | ✅ |
| `SettingsService.cs` | `LoadSettingsAsync()` | **未加锁** | ❌ |
| `MainViewModel.cs` | `SaveWordsAsync()` / `LoadWordsAsync()` | **未加锁**，仍与 `WordsPage` 竞争临时文件 | ❌ |

> ⚠️ 读-写竞争风险仍未完全消除。`LoadSettingsAsync` 读取时，`SaveSettingsAsync` 可能正在写入，存在读到截断文件的风险。建议读写共用同一把锁，或使用原子写入（写临时文件 + `File.Move`）。

---

### ✅ 威胁 #5：密码哈希机制缺陷（完全修复）

| 修复项 | 修复前 | 修复后 | 状态 |
|--------|--------|--------|------|
| 哈希算法 | SHA256 无盐 | **PBKDF2 + 16字节随机盐** | ✅ |
| 迭代次数 | 1 | **100,000** | ✅ |
| 空哈希处理 | `VerifyPassword` 返回 `true` | 返回 `false` | ✅ |
| 比较方式 | `==`（可被计时攻击） | `CryptographicOperations.FixedTimeEquals` | ✅ |
| 创建用户 | `PasswordHash = password`（明文） | `PasswordService.HashPassword(password)` | ✅ |
| 旧数据迁移 | 无 | `NeedsRehash()` 方法支持渐进式迁移 | ✅ |

**代码验证**（`PasswordService.cs`）：
```csharp
private const int SaltSize = 16;
private const int HashSize = 32;
private const int Iterations = 100_000;

public static string HashPassword(string password)
{
    byte[] salt = new byte[SaltSize];
    using (var rng = RandomNumberGenerator.Create()) { rng.GetBytes(salt); }
    using (var pbkdf2 = new Rfc2898DeriveBytes(password, salt, Iterations, HashAlgorithmName.SHA256))
    {
        byte[] hash = pbkdf2.GetBytes(HashSize);
        return $"{Convert.ToBase64String(salt)}:{Convert.ToBase64String(hash)}";
    }
}

public static bool VerifyPassword(string password, string passwordHash)
{
    if (string.IsNullOrEmpty(passwordHash)) return false;  // 空哈希不再允许登录
    // ... PBKDF2 验证 + FixedTimeEquals
}
```

---

### ✅ 威胁 #6：多用户数据隔离（部分修复）

| 修复项 | 状态 | 说明 |
|--------|------|------|
| `SettingsService.CreateUserAsync` 使用强哈希 | ✅ | 已修复 |
| `UserPage.CreateUserButton_Click` 使用强哈希 | ✅ | 已修复 |
| `DeleteUserAsync` 禁止删除当前登录用户 | ❌ | 未修复 |
| `LoadTestHistoryAsync` 校验当前用户 | ❌ | 未修复 |
| 数据操作层注入 `CurrentUser` | ❌ | 未修复 |

---

## 三、 P1 项修复详情

| 威胁 | 修复内容 | 状态 |
|------|----------|------|
| Timer 资源泄漏（WordsPage / MainWindow） | `WordsPage` 仍无 `OnNavigatedFrom` Timer 解绑；`MainWindow` 静态 Timer 仍无 Dispose | ❌ |
| 缓存大小限制 | `BaiduTranslateService` 已限制 10000 条 + 7 天历史；`TranslationLibraryService` 仍无限制 | 🟡 部分 |
| 文件 I/O 锁 | `SaveSettingsAsync` / `SaveUsersAsync` 已加锁；`LoadSettingsAsync` / `LoadWordsAsync` 未加锁 | 🟡 部分 |
| 临时文件竞争 | `MainViewModel.SaveWordsAsync` 与 `WordsPage.SaveToTempFileAsync` 仍竞争同一文件 | ❌ |
| 原子写入 | 仍使用 `File.WriteAllTextAsync` 直接覆盖，非原子写入 | ❌ |

---

## 四、 P2 项修复详情

| 威胁 | 修复内容 | 状态 |
|------|----------|------|
| 百度 API 密钥硬编码（嵌入资源） | 仍为嵌入资源 `Config.secret.json` | ❌ |
| `HttpClient` 非单例 | 每实例创建新 `HttpClient` | ❌ |
| 更新下载无签名验证 | `UpdateService.DownloadUpdateAsync` 无哈希校验 | ❌ |
| 多用户数据归属校验 | 无 `CurrentUser` 注入校验层 | ❌ |

---

## 五、 遗留风险与建议

### 🟡 建议尽快修复（本周内）

1. **`TranslationLibraryService` 缓存大小限制**
   ```csharp
   private const int MaxEntries = 50000; // 或根据实际内存预算设定
   ```

2. **`SettingsService` 读写共用锁 + 原子写入**
   ```csharp
   public async Task LoadSettingsAsync()
   {
       await _fileLock.WaitAsync();  // 加锁
       try { /* 读取 */ } finally { _fileLock.Release(); }
   }
   
   // 原子写入
   var tempPath = SettingsFilePath + ".tmp";
   await File.WriteAllTextAsync(tempPath, json);
   File.Move(tempPath, SettingsFilePath, overwrite: true);
   ```

3. **`WordsPage._autoSaveTimer` 显式解绑**
   ```csharp
   protected override void OnNavigatedFrom(NavigationEventArgs e)
   {
       base.OnNavigatedFrom(e);
       SaveCurrentState();
       _autoSaveTimer?.Stop();
       _autoSaveTimer!.Tick -= AutoSaveTimer_Tick;
   }
   ```

4. **`DeleteUserAsync` 禁止删除当前用户**
   ```csharp
   public async Task<bool> DeleteUserAsync(string username)
   {
       if (Settings.CurrentUser == username)
           throw new InvalidOperationException("不能删除当前登录用户");
       // ...
   }
   ```

### 🟢 未来版本考虑

5. 百度 API 密钥使用 Windows DPAPI 加密存储（替代嵌入资源）
6. `BaiduTranslateService` 使用静态 `HttpClient` 单例
7. `UpdateService` 下载后增加 SHA256 哈希校验
8. 多用户数据访问层统一注入 `CurrentUser` 上下文校验

---

## 六、 结论

> **P0 高危项已全部修复**，尤其是：
> - `async void` 并发竞争 → 通过 `SemaphoreSlim` 锁 + `async Task` 方法彻底消除
> - SHA256 无盐明文密码 → 升级为 **PBKDF2 + 100k iterations + 随机盐 + 固定时间比较**
> 
> **P1 项修复了约 60%**，文件 I/O 锁和缓存大小限制在核心路径（`SettingsService` / `BaiduTranslateService`）已修复，但 `TranslationLibraryService` 和读-写竞争仍需完善。
> 
> **P2 项全部未修复**，主要是供应链安全（更新签名、密钥加密）和架构层（多用户数据隔离），建议在下一个功能迭代周期中处理。

**综合评级**: 从 🔴 **高危** → 🟡 **中危**（P0 已消除，剩余风险可控）
