# NNU InterConnector

校园网P2P直连互联工具 - 帮助同学免费、低延迟、高带宽的通过校园网内网以直连的方式互联

## 项目简介

NNU InterConnector 是一个专为校园网环境设计的P2P直连工具，解决校园网AP隔离和跨网段通信问题。通过信令服务器辅助发现，结合智能路由和防火墙配置，实现校园网内设备的高效P2P互联。

### 核心特性

- **免费开源**: 完全免费，源代码开放
- **低延迟**: 内网直连，无需中转服务器
- **高带宽**: 充分利用校园网内网带宽
- **智能配置**: 自动识别网络环境，智能配置路由和防火墙
- **一键操作**: 图形化界面，一键连接/断开
- **自动清理**: 断开时自动清理所有配置，不留痕迹

## 系统架构

### 两个核心组件

1. **Server** (ASP.NET Core 8.0 SignalR服务器)
   - 信令服务器，用于辅助发现和IP交换
   - 心跳检测，自动清理离线客户端
   - 内存存储，无数据库依赖
   - 双端口设计：HTTP API (8080) + WebSocket (8081)

2. **Client** (WPF桌面应用)
   - 图形化用户界面，Material Design风格
   - 显示自己的6位数字ID和校园网IP
   - 输入对方ID发起连接
   - 管理已建立的连接列表
   - 实时日志输出
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

### 2. 获取ID

客户端启动后自动完成：
- 检测校园网IP（10.20.x.x或10.30.x.x）
- 生成UUID并注册到服务器
- 获取6位数字ID（界面顶部显示）

### 3. 发起连接

- 在输入框中输入对方的6位ID
- 点击"连接"按钮
- 等待对方确认...

### 4. 确认请求

收到连接请求时：
- 弹出确认对话框，显示对方ID和IP
- 点击"是"接受连接
- 点击"否"拒绝连接

### 5. 管理连接

**连接列表**: 显示所有已建立的连接
- 对方ID
- 对方IP
- 连接状态（带颜色标识）
- 连接时间

**状态颜色说明**:
- 🟢 **绿色**: 已连接
- 🟠 **橙色**: 连接中
- ⚫ **灰色**: 断开中
- 🔴 **红色**: 配置失败

**操作**:
- **复制ID**: 点击ID旁边的"复制"按钮
- **断开连接**: 选中连接后点击"断开选中连接"

### 6. 查看日志

底部日志区域实时显示：
- 初始化状态
- 注册结果
- 连接请求和响应
- 防火墙和路由配置信息
- 错误信息

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

// 请求连接
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

// 连接失败
ConnectionFailed(string message)

// 连接被拒绝
ConnectionRejected(int rejecterId)

// 对方断开连接
PeerDisconnected(int peerId, string peerIp)
```

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
4. 查看日志中的详细错误信息

### 连接请求失败

**现象**: 输入对方ID后连接失败  
**原因**:
- 对方ID错误
- 对方不在线
- 服务器连接断开

**解决**:
1. 确认对方ID是6位数字
2. 确认对方已启动客户端并获取到ID
3. 检查客户端日志中的服务器连接状态

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
