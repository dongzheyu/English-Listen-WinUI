# 配置文件结构更新说明

## 概述

本次更新重构了英语听写练习系统的配置文件结构，实现了以下目标：

1. 将JSON配置文件保存到config文件夹
2. 用户和听写数据保存到config下的user目录，按用户名新建文件夹存储
3. 开启加密时加密config目录下所有文件，创建hash.txt存储密钥哈希值
4. 关闭加密时自动删除hash.txt文件

## 新的目录结构

```
应用程序目录/
├── config/                    # 系统配置文件目录
│   ├── settings.json          # 系统全局设置
│   ├── wordlist_groups.ini    # 词库分组配置
│   ├── hash.txt               # 加密密码哈希（加密时创建）
│   └── users/                 # 用户数据根目录
│       ├── user1/             # 用户1的个人目录
│       │   ├── settings.json  # 用户1的个人设置
│       │   └── test_history.json  # 用户1的测试历史
│       └── user2/             # 用户2的个人目录
│           ├── settings.json  # 用户2的个人设置
│           └── test_history.json  # 用户2的测试历史
└── wordlist/                  # 词库文件目录（保持不变）
```

## 主要变更

### 1. SettingsService.cs

- **新的文件路径配置**：
  - `SettingsFilePath`: `config/settings.json`
  - `WordlistGroupsFilePath`: `config/wordlist_groups.ini`
  - `HashFilePath`: `config/hash.txt`
  - 用户特定路径：`config/users/{username}/settings.json` 和 `config/users/{username}/test_history.json`

- **加密功能**：
  - `EnableEncryptionAsync(password)`: 启用加密并加密所有配置文件
  - `DisableEncryptionAsync(password)`: 禁用加密并解密所有配置文件
  - `EncryptAllConfigFilesAsync(password)`: 加密所有配置文件
  - `DecryptAllConfigFilesAsync(password)`: 解密所有配置文件
  - `VerifyEncryptionPasswordAsync(password)`: 验证加密密码
  - `LoadSettingsWithPasswordAsync(password)`: 使用密码加载加密的设置

- **数据迁移**：
  - `MigrateOldDataAsync()`: 自动迁移旧的配置文件到新结构
  - 支持从旧位置迁移：`settings.json`, `users.json`, `test_history.json`, `wordlist_groups.ini`

### 2. 用户数据存储

- 每个用户现在有自己的独立目录
- 用户设置和测试历史分别存储在各自的目录中
- 支持多用户管理和数据隔离

### 3. UI更新

- **SettingsPage.xaml**: 更新了加密操作界面
  - **删除了** "启用数据加密"复选框（因无法处理密码输入）
  - **添加了** "启用加密"按钮
  - **添加了** "禁用加密"按钮
  - 按钮根据加密状态自动显示/隐藏

- **SettingsPage.xaml.cs**: 添加了加密操作的事件处理
  - 密码输入对话框（包含密码确认）
  - 密码验证
  - 加密状态管理
  - 按钮状态自动更新

## 向后兼容性

- 系统会自动检测并迁移旧的配置文件到新结构
- 现有的用户数据和设置会被保留
- 所有现有的API调用都已更新以支持新的路径结构

## 使用方法

### 启用加密

1. 在设置页面点击"启用加密"按钮
2. 输入并确认加密密码
3. 系统将加密所有配置文件并创建hash.txt

### 禁用加密

1. 在设置页面点击"禁用加密"按钮
2. 输入当前加密密码进行验证
3. 系统将解密所有配置文件并删除hash.txt

### 数据迁移

- 系统在启动时会自动调用`MigrateOldDataAsync()`
- 如果检测到旧的配置文件，会自动迁移到新结构
- 迁移完成后，旧的配置文件会被保留（注释掉的删除代码）

## 安全特性

- 使用AES-256加密算法
- 密码使用SHA256哈希存储
- 每个文件独立加密，增强安全性
- 加密文件格式：`ENCRYPTED:base64_encoded_data`
- **修复了禁用加密时的解密问题**：现在确保所有文件都正确解密

## 新增功能

### 启动时加密状态检查

**功能**：每次重新启动程序后自动检查配置文件加密状态

**实现**：
1. `CheckAndEncryptConfigFilesAsync()`: 检查所有配置文件是否已加密
2. `EncryptUnencryptedFilesAsync(password)`: 加密未加密的文件
3. 自动检测并报告未加密文件

**检查范围**：
- 系统设置文件 (`config/settings.json`)
- 词库分类文件 (`config/wordlist_groups.ini`)
- 所有用户设置文件 (`config/users/{username}/settings.json`)
- 所有用户测试历史文件 (`config/users/{username}/test_history.json`)

### 启动时密码输入流程

**功能**：启动程序时自动检测加密状态，要求用户输入解密密码

**实现**：
1. `App.xaml.cs` 中检查加密状态
2. 显示密码输入对话框
3. 使用密码解密用户数据
4. 将解密后的用户数据缓存显示

**核心方法**：
- `LoadUsersWithPasswordAsync(password)`: 使用密码加载加密的用户数据
- `LoadUsersFromListAsync(users)`: 将用户数据加载到ViewModel中显示
- `CurrentPassword` 属性：临时存储当前会话的密码

**安全措施**：
- 只在加密启用状态下进行检查
- 不自动加密（需要用户输入密码）
- 详细日志记录未加密文件
- 密码仅在内存中临时存储

## 修复的问题

### 禁用加密后文件未正确解密

**问题**：禁用加密时，词库分类文件和用户配置文件没有正确解密恢复

**修复措施**：
1. 增强了`DecryptAllConfigFilesAsync`方法的错误处理和日志记录
2. 添加了`AreAllFilesDecryptedAsync`方法验证所有文件是否已解密
3. 在禁用加密过程中添加了文件解密验证步骤
4. 改进了UI错误提示，提供更详细的解密失败信息

**验证机制**：
- 解密后自动检查所有配置文件是否已正确解密
- 如果解密失败，会抛出详细错误信息
- 用户界面显示详细的错误原因和可能的解决方案

## 错误处理

- 添加了详细的错误日志记录
- 用户友好的错误提示
- 文件操作异常处理
- 密码验证失败处理

## 构建状态

✅ 项目构建成功
✅ 所有编译错误已修复
✅ 警告已处理
✅ 功能完整可用