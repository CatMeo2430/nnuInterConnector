# NNU InterConnector

校园网P2P直连互联工具 - 帮助同学免费、低延迟、高带宽的通过校园网内网以直连的方式互联

## 项目简介

NNU InterConnector 是一个专为校园网环境设计的P2P直连工具，解决校园网AP隔离和跨网段通信问题。通过信令服务器辅助发现，结合智能路由和防火墙配置，实现校园网内设备的高效P2P互联。

### 核心特性

- **免费开源**: 完全免费，源代码开放
- **低延迟**: 内网直连，无需中转服务器
- **高带宽**: 充分利用校园网内网带宽
- **智能配置**: 自动识别网络环境，智能配置路由和防火墙
- **现代UI**: WinUI3风格界面，美观易用
- **连接模式**: 支持手动、自动同意、自动拒绝三种模式
- **安全机制**: 30秒超时、重复请求防护、冷却期限制
- **自动清理**: 断开时自动清理所有配置，不留痕迹
- **文件日志**: 运行时日志自动保存到Logs文件夹

## 系统架构

### 两个核心组件

1. **Server** (ASP.NET Core 8.0 SignalR服务器)
   - 信令服务器，用于辅助发现和IP交换
   - 心跳检测，自动清理离线客户端
   - 请求管理：防止重复请求、30秒超时、冷却期限制
   - 内存存储，无数据库依赖
   - 双端口设计：HTTP API (8080) + WebSocket (8081)

2. **Client** (WPF桌面应用)
   - 图形化用户界面，WinUI3风格设计
   - 显示自己的6位数字ID和校园网IP
   - 三态切换开关：手动/自动同意/自动拒绝
   - 进度条式连接协商界面
   - 管理已建立的连接列表
   - 自定义对话框（非系统弹窗）
   - 文件日志记录（Logs文件夹）
   - **需要管理员权限**（配置网络和防火墙）

## 工作原理

### 网络环境

校园网IPv4有两个网段：
- `10.20.x.x` (网关: 10.20.0.1)
- `10.30.x.x` (网关: 10.30.0.1)

**同网段**: 有AP隔离，需要强制路由绕过  
**跨网段**: 可以直接IP直连

### 强制路由方案

同网段环境下，通过强制路由绕过AP隔离：

```bash
route add <对方IP> mask 255.255.255.255 <网关IP>
```

- `10.20.x.x` → 网关 `10.20.0.1`
- `10.30.x.x` → 网关 `10.30.0.1`

### 连接流程

1. **注册**: 客户端启动 → 生成UUID → HTTP注册 → 获取6位ID
2. **发现**: WebSocket保持连接 → 30秒心跳 → 维持在线状态
3. **请求**: 输入对方ID → 服务器转发请求 → 对方确认
4. **建立**: 双方接收对方IP → 配置防火墙和路由 → 直连通道建立
5. **通信**: P2P直连通道建立 → 内网高速通信
6. **断开**: 一键断开 → 自动清理防火墙规则和路由

## 快速开始

### 环境要求

- **Server**: .NET 8.0 SDK
- **Client**: Windows 10/11, .NET 8.0 Runtime, 管理员权限
- **网络**: 校园网环境 (IP为10.20.x.x或10.30.x.x)

### 构建项目

#### 1. 构建Server

```bash
cd Server
dotnet build -c Release
dotnet run -c Release
```

服务器配置在`appsettings.json`：
```json
{
  "ServerConfig": {
    "IpAddress": "10.20.214.145",
    "HttpPort": 8080,
    "WebSocketPort": 8081
  }
}
```

#### 2. 构建Client

```bash
cd Client
dotnet build -c Release
```

输出文件：`Client/bin/Release/net8.0-windows/Client.exe`

### 部署

#### 服务器部署

```bash
cd Server
dotnet publish -c Release -o publish
```

将publish文件夹部署到服务器 `10.20.214.145`

#### 客户端部署

将以下文件分发给用户：
- `Client.exe` (WPF客户端，需要管理员权限)

## 使用指南

### 1. 启动客户端

双击 `Client.exe` 启动  
Windows会弹出UAC提示，点击"是"授予管理员权限

客户端启动后自动完成：
- 检测校园网IP（10.20.x.x或10.30.x.x）
- 生成UUID并注册到服务器
- 获取6位数字ID（界面顶部显示）

### 2. 连接模式设置

界面右上角有三态切换开关：
- **手动控制**: 收到连接请求时弹出确认对话框
- **自动同意**: 自动接受所有连接请求
- **自动拒绝**: 自动拒绝所有连接请求

### 3. 发起连接

点击"发起互联"按钮：
- 在弹出的窗口中输入对方的6位ID
- 点击"开始连接"
- 等待进度条显示连接进度...

**输入验证**:
- ID必须是6位数字（100000-999999）
- 不能连接到自己（会弹出提示）
- 如果已建立连接，会提示"已建立互联"

### 4. 连接协商过程

进度条会显示以下步骤：
1. **发送请求**: 向服务器发送连接请求
2. **等待响应**: 等待对方确认（30秒超时）
3. **配置网络**: 配置防火墙和路由规则
4. **连接成功**: 建立P2P直连通道

**主动放弃**: 在连接过程中，可以关闭窗口主动取消请求

### 5. 确认请求（手动模式）

收到连接请求时，弹出非模态对话框：
- 显示对方ID和IP地址
- 点击"确认"接受连接
- 点击"取消"拒绝连接
- 30秒无操作自动超时

### 6. 管理连接

**连接列表**: 显示所有已建立的连接
- 对方ID（可点击复制）
- 对方IP（可点击复制）
- 连接状态（可点击复制）
- 连接时间（可点击复制）

**状态颜色说明**:
- 🟢 **绿色**: 已连接
- 🟠 **橙色**: 连接中
- ⚫ **灰色**: 断开中
- 🔴 **红色**: 配置失败

**操作**:
- **断开连接**: 点击"断开"按钮，确认后断开连接

### 7. 查看日志

所有日志自动保存到 `Logs` 文件夹：
- 按日期命名日志文件（yyyy-MM-dd.log）
- 包含时间戳和详细操作记录
- 可用于故障排查

## 技术细节

### 服务端API

#### 注册客户端
```http
POST http://10.20.214.145:8080/api/Registration
Headers:
  X-Client-UUID: <uuid>
  X-Client-IP: <ip-address>
```

响应：200 OK

#### SignalR Hub
```
ws://10.20.214.145:8081/interconnectionHub
Headers:
  X-Client-UUID: <uuid>
```

**Hub方法**:
```csharp
// 注册客户端
RegisterClient(string uuid)

// 请求连接（带防重复和冷却期检查）
RequestConnection(int targetId)

// 接受连接
AcceptConnection(int requesterId)

// 拒绝连接
RejectConnection(int requesterId)

// 断开连接
DisconnectPeer(int peerId)
```

**客户端事件**:
```csharp
// 注册成功
RegistrationSuccess(int clientId)

// 收到连接请求
ConnectionRequest(int requesterId, string requesterIp)

// 连接已建立
ConnectionEstablished(int peerId, string peerIp)

// 连接失败（错误码）
ConnectionFailed(int errorCode)
// 错误码: 1=目标不存在, 2=目标不在线, 3=连接超时, 4=重复请求

// 连接被拒绝
ConnectionRejected(int rejecterId)

// 连接超时
ConnectionTimeout(int requesterId)

// 对方断开连接
PeerDisconnected(int peerId, string peerIp)
```

### 安全机制

#### 1. 请求限制
- 每个客户端同时只能有一个待处理的连接请求
- 新的请求会被直接拒绝（错误码4）

#### 2. 冷却期机制
- 被拒绝后（主动拒绝），需要等待1分钟才能再次请求同一目标
- 超时或重复请求不计入拒绝次数

#### 3. 永久拒绝
- 同一请求者被拒绝3次后，永久拒绝所有请求
- 永久拒绝的请求不会发送给被请求者

#### 4. 30秒超时
- 连接请求30秒未响应自动超时
- 双方都会收到超时通知

### 客户端服务

#### FirewallService
```csharp
// 添加防火墙规则
bool AddFirewallRule(string ipAddress)

// 删除防火墙规则
bool RemoveFirewallRule(string ipAddress)
```

#### RouteService
```csharp
// 添加路由
bool AddRoute(string destinationIp, string gateway)

// 删除路由
bool RemoveRoute(string destinationIp)
```

#### NetworkService
```csharp
// 获取校园网IP
string GetCampusNetworkIp()

// 判断是否同网段
bool IsSameSubnet(string ip1, string ip2)

// 获取网关
string GetGatewayForIp(string ipAddress)
```

### 数据存储

服务端使用内存数组存储（长度1,000,000）：

```csharp
class ClientInfo
{
    public string Uuid { get; set; }              // 客户端UUID
    public string IpAddress { get; set; }         // IP地址
    public DateTime LastHeartbeat { get; set; }   // 最后心跳时间
    public string ConnectionId { get; set; }      // SignalR连接ID
}
```

通过ID作为数组下标快速访问，心跳超时2分钟后自动清理。

## 故障排除

### 客户端无法启动（UAC问题）

**现象**: 启动Client.exe时没有任何反应  
**原因**: 需要管理员权限，但UAC被禁用或阻止  
**解决**:
1. 右键Client.exe → "以管理员身份运行"
2. 检查Windows UAC设置
3. 确保在管理员账户下运行

### 客户端无法获取ID

**现象**: ID显示"未连接"或"检测中..."  
**原因**: 
- 未连接到校园网
- 服务器不可达
- 防火墙阻止

**解决**:
1. 检查网络连接是否为校园网（IP应为10.20.x.x或10.30.x.x）
2. 检查服务器地址配置是否正确
3. 检查Windows防火墙是否阻止Client.exe
4. 查看Logs文件夹中的日志文件

### 连接请求失败（错误码说明）

**现象**: 连接失败并显示错误信息  
**错误码**:
- **错误码 1**: 目标ID不存在
  - 确认对方ID输入正确
  - 确认对方已启动客户端
  
- **错误码 2**: 目标不在线
  - 对方可能已断开连接
  - 请对方重新启动客户端
  
- **错误码 3**: 连接超时
  - 对方30秒内未响应
  - 请对方检查连接模式（手动/自动）
  
- **错误码 4**: 重复请求
  - 你已经有一个待处理的请求
  - 等待当前请求完成或关闭连接窗口

### 防火墙配置失败

**现象**: 日志显示"防火墙配置失败"  
**原因**:
- 未以管理员权限运行
- Windows防火墙服务未运行
- IP地址格式错误

**解决**:
1. 确保以管理员身份运行Client.exe
2. 检查Windows Defender防火墙服务是否正在运行
3. 确认对方IP格式正确（10.20.x.x或10.30.x.x）

### 同网段无法通信

**现象**: 连接成功但无法ping通（同网段）  
**原因**: 强制路由未正确配置  
**解决**:
1. 检查日志是否显示"强制路由已配置"
2. 手动检查路由表: `route print`  
3. 确认路由存在: `10.xxx.xxx.xxx  255.255.255.255      10.x.0.1`
4. 如果路由不存在，尝试手动添加测试

### 跨网段无法通信

**现象**: 连接成功但无法ping通（跨网段）  
**原因**: 
- 对方防火墙阻止
- 网络设备限制

**解决**:
1. 确认双方都已接受连接
2. 检查双方防火墙规则是否添加成功
3. 尝试ping对方网关确认网络连通性
4. 联系网络管理员确认是否有跨网段限制

### 断开连接后无法重新连接

**现象**: 断开连接后，再次连接同一ID失败  
**原因**: 防火墙规则或路由未完全清理  
**解决**:
1. 检查日志确认清理是否成功
2. 手动检查防火墙规则: `netsh advfirewall firewall show rule name=all`
3. 手动检查路由表: `route print`
4. 重启Client.exe尝试重新连接

### 冷却期限制

**现象**: 被拒绝后1分钟内无法再次请求同一目标  
**原因**: 安全机制，防止频繁骚扰  
**解决**:
1. 等待1分钟后再尝试
2. 检查Logs文件夹中的日志确认剩余冷却时间
3. 这是正常行为，不是错误

### 永久拒绝

**现象**: 被拒绝3次后，无法再向同一目标发送请求  
**原因**: 对方多次拒绝，系统自动永久屏蔽  
**解决**:
1. 联系对方确认是否愿意连接
2. 这是安全保护机制，无法绕过
3. 建议更换ID或让对方发起请求

### 日志文件位置

**位置**: `Client.exe` 所在目录的 `Logs` 文件夹  
**文件名**: `yyyy-MM-dd.log`（按日期命名）  
**内容**: 包含所有操作记录和错误信息  
**用途**: 故障排查和问题反馈

## 安全说明

- **管理员权限**: 客户端需要管理员权限配置网络和防火墙，这是正常需求
- **最小权限**: 仅放行指定IP的入站连接，不影响其他网络访问
- **自动清理**: 断开连接时自动删除防火墙规则和路由，不留后门
- **无数据收集**: 服务器不收集或存储任何用户隐私数据
- **端到端**: 所有数据传输为P2P直连，不经过服务器中转

## 性能指标

- **注册响应时间**: < 100ms
- **连接建立时间**: < 3s（包括对方确认）
- **心跳间隔**: 30秒
- **超时清理**: 2分钟
- **内存占用**: Server < 50MB, Client < 100MB

## 开发指南

### 项目结构

```
nnuInterConnector/
├── Server/                          # ASP.NET Core SignalR服务器
│   ├── Controllers/
│   │   └── RegistrationController.cs
│   ├── Hubs/
│   │   └── InterconnectionHub.cs
│   ├── Models/
│   │   └── ClientInfo.cs
│   ├── Program.cs
│   ├── appsettings.json
│   └── Server.csproj
├── Client/                          # WPF客户端
│   ├── Models/
│   │   ├── ConnectionInfo.cs
│   │   └── ...
│   ├── Services/
│   │   ├── SignalRService.cs
│   │   ├── FirewallService.cs
│   │   ├── RouteService.cs
│   │   └── NetworkService.cs
│   ├── ViewModels/
│   │   └── MainViewModel.cs
│   ├── MainWindow.xaml
│   ├── App.xaml
│   ├── app.manifest              # 管理员权限配置
│   └── Client.csproj
├── Document/                       # 文档
├── Release/                        # 发布文件
├── LICENSE
├── README.md
└── nnuInterConnector.sln
```

### 技术栈

- **Server**: ASP.NET Core 8.0, SignalR, Serilog
- **Client**: WPF, .NET 8.0, CommunityToolkit.Mvvm, Material Design
- **语言**: C# 12
- **协议**: SignalR over WebSocket, RESTful API

### 扩展开发

#### 添加新功能

1. **修改Server**: 更新Hub方法或API端点
2. **修改Client**: 在SignalRService中处理新事件
3. **UI更新**: 修改MainViewModel和MainWindow.xaml

#### 自定义服务器地址

修改`Server/appsettings.json`:
```json
{
  "ServerConfig": {
    "IpAddress": "你的服务器IP",
    "HttpPort": 8080,
    "WebSocketPort": 8081
  }
}
```

#### 修改心跳间隔

**Server**: `Program.cs`中的Timer间隔  
**Client**: `SignalRService.cs`中的心跳间隔

## 版本历史

### v1.1.0 (2024-12-20)
- ✅ WinUI3风格UI界面
- ✅ 进度条式连接协商
- ✅ 三态切换开关（手动/自动同意/自动拒绝）
- ✅ 文件日志系统（Logs文件夹）
- ✅ 自定义对话框（非系统弹窗）
- ✅ 30秒连接超时机制
- ✅ 重复请求防护
- ✅ 已连接检查
- ✅ 发起者主动放弃功能
- ✅ 服务端请求限制（单请求）
- ✅ 冷却期机制（1分钟）
- ✅ 永久拒绝机制（3次拒绝）

### v1.0.0 (2024-12-19)
- ✅ 初始版本发布
- ✅ SignalR信令服务器
- ✅ WPF图形化客户端
- ✅ 防火墙自动配置
- ✅ 强制路由绕过AP隔离
- ✅ 心跳检测和自动清理
- ✅ 管理员权限管理

## 许可证

MPL License - 详见 [LICENSE](LICENSE) 文件

## 贡献指南

欢迎提交Issue和Pull Request！

### 开发环境

1. 安装 [.NET 8.0 SDK](https://dotnet.microsoft.com/download)
2. 使用 Visual Studio 2022 或 JetBrains Rider
3. 克隆仓库: `git clone https://github.com/CatMeo2430/nnuInterConnector.git`
4. 打开解决方案: `nnuInterConnector.sln`

### 提交规范

- 使用清晰的提交信息
- 添加适当的注释
- 保持代码风格一致
- 更新README（如有必要）

## 联系方式

- **项目地址**: https://github.com/CatMeo2430/nnuInterConnector
- **问题反馈**: [GitHub Issues](https://github.com/CatMeo2430/nnuInterConnector/issues)
- **邮箱**: CatMeo2430@outlook.com

## 致谢

感谢南京师范大学校园网提供的优秀网络环境！

## 免责声明

本工具仅供学习和合法用途，请遵守校园网使用规定和相关法律法规。使用本工具即表示您同意承担所有相关责任。

---

**Made with ❤️ for NNU Students**
