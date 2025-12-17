# NNU InterConnector

校园网P2P直连互联工具 - 帮助同学免费、低延迟、高带宽的通过校园网内网以直连的方式互联

## 项目简介

NNU InterConnector 是一个专为校园网环境设计的P2P直连工具，解决校园网AP隔离和跨网段通信问题。

### 核心特性

- **免费开源**: 完全免费，源代码开放
- **低延迟**: 内网直连，无需中转服务器
- **高带宽**: 充分利用校园网内网带宽
- **自动配置**: 自动处理防火墙和路由配置
- **简单易用**: 一键连接，图形化界面

## 系统架构

### 三个核心组件

1. **Server** (ASP.NET Core 8.0 SignalR服务器)
   - 信令服务器，用于辅助发现和IP交换
   - 心跳检测，自动清理离线客户端
   - 内存存储，无数据库依赖
   - 监听地址: `http://120.55.67.157:8080` (HTTP) + `ws://120.55.67.157:8081` (WebSocket)

2. **Client** (WPF桌面应用)
   - 图形化用户界面
   - 显示自己的6位数字ID
   - 输入对方ID发起连接
   - 管理已建立的连接
   - 实时日志输出

3. **Helper** (管理员权限控制台程序)
   - 自动申请管理员权限
   - 配置Windows防火墙规则
   - 管理路由表（绕过AP隔离）
   - 被Client自动调用

## 工作原理

### 网络环境

校园网IPv4有两个网段：
- `10.20.x.x` (网关: 10.20.0.1)
- `10.30.x.x` (网关: 10.30.0.1)

**同网段**: 有AP隔离，需要强制路由绕过
**跨网段**: 可以直接IP直连

### 连接流程

1. **注册**: 客户端启动 → 生成UUID → HTTP注册 → 获取6位ID
2. **发现**: WebSocket保持连接 → 30秒心跳更新IP
3. **请求**: 输入对方ID → 服务器转发请求 → 对方确认
4. **建立**: 双方接收对方IP → Helper配置防火墙和路由
5. **通信**: 直连通道建立 → 开始P2P通信
6. **断开**: 一键断开 → 自动清理防火墙和路由

### 强制路由方案

同网段环境下，通过强制路由绕过AP隔离：

```bash
route add <对方IP> mask 255.255.255.255 <网关IP>
```

- `10.20.x.x` → 网关 `10.20.0.1`
- `10.30.x.x` → 网关 `10.30.0.1`

## 快速开始

### 环境要求

- .NET 8.0 SDK
- Windows 10/11 (WPF客户端)
- 校园网环境 (10.20.x.x 或 10.30.x.x)

### 构建项目

#### 1. 构建Server

```bash
cd Server
dotnet build
dotnet run
```

服务器将监听：
- HTTP API: `http://120.55.67.157:8080`
- SignalR Hub: `ws://120.55.67.157:8081`

#### 2. 构建Helper

```bash
cd Helper
dotnet build
```

Helper.exe将自动请求管理员权限

#### 3. 构建Client

使用Visual Studio 2022打开解决方案：
1. 打开 `nnuInterConnector.sln`
2. 选择Client项目
3. 右键 → 设为启动项目
4. F5运行或构建

### 部署

#### 服务器部署

```bash
cd Server
dotnet publish -c Release -o publish
```

将publish文件夹部署到服务器 `120.55.67.157`

#### 客户端部署

将以下文件分发给用户：
- `Client.exe` (WPF客户端)
- `Helper.exe` (管理员权限助手)
- `Helper.exe.config`

确保两个exe在同一目录下

## 使用指南

### 1. 启动客户端

双击 `Client.exe` 启动

### 2. 获取ID

客户端启动后：
- 自动检测校园网IP
- 生成UUID并注册到服务器
- 获取6位数字ID（界面顶部显示）

### 3. 发起连接

- 在输入框中输入对方的6位ID
- 点击"连接"按钮
- 等待对方确认

### 4. 确认请求

收到连接请求时：
- 弹出确认对话框
- 显示对方ID和IP
- 点击"是"接受或"否"拒绝

### 5. 管理连接

**连接列表**: 显示所有已建立的连接
- 对方ID
- 对方IP
- 连接状态
- 连接时间

**断开连接**: 
- 选中要断开的连接
- 点击"断开选中连接"
- 自动清理防火墙和路由

### 6. 查看日志

底部日志区域实时显示：
- 注册状态
- 连接请求
- 配置信息
- 错误信息

## 技术细节

### 服务端API

#### 注册客户端
```http
POST http://120.55.67.157:8080/api/Registration
Header: X-Client-UUID: <uuid>
```

#### SignalR Hub
```
ws://120.55.67.157:8081/interconnectionHub
Header: X-Client-UUID: <uuid>
```

**Hub方法:**
- `RegisterClient(uuid, ipAddress)`
- `UpdateHeartbeat(ipAddress)`
- `RequestConnection(targetId)`
- `AcceptConnection(requesterId)`
- `RejectConnection(requesterId)`

**客户端事件:**
- `RegistrationSuccess(clientId)`
- `ConnectionRequest(requesterId, requesterIp)`
- `ConnectionEstablished(peerId, peerIp)`
- `ConnectionFailed(message)`
- `ConnectionRejected(rejecterId)`

### Helper命令行

```bash
# 添加配置
Helper.exe add <ip_address> [gateway]

# 删除配置
Helper.exe remove <ip_address>

# 示例
Helper.exe add 10.20.1.100 10.20.0.1
Helper.exe add 10.30.5.200
Helper.exe remove 10.20.1.100
```

### 数据存储

服务端内存数组（长度1,000,000）：
```csharp
struct ClientInfo {
    string Uuid;
    string IpAddress;
    DateTime LastHeartbeat;
    string ConnectionId;
}
```

通过ID作为数组下标快速访问，心跳超时2分钟后自动清理。

## 故障排除

### 客户端无法获取ID

1. 检查网络连接
2. 确认在校园网环境（IP为10.20.x.x或10.30.x.x）
3. 检查服务器是否运行
4. 查看防火墙是否阻止连接

### 连接请求失败

1. 确认对方ID正确
2. 对方必须在线且已启动客户端
3. 检查服务器连接状态

### Helper配置失败

1. 确保Helper.exe有管理员权限
2. 检查Windows防火墙服务是否运行
3. 确认IP地址格式正确

### 同网段无法通信

1. 确认Helper已配置强制路由
2. 检查路由表: `route print`
3. 确认网关地址正确（10.20.0.1或10.30.0.1）

## 安全说明

- Helper.exe需要管理员权限配置网络和防火墙
- 仅放行指定IP的入站连接
- 断开时自动清理防火墙规则
- 不收集或上传任何用户数据
- 所有通信端到端加密（由应用程序自行实现）

## 许可证

MIT License - 详见 LICENSE 文件

## 贡献

欢迎提交Issue和Pull Request！

## 联系方式

如有问题或建议，请通过GitHub Issues联系。

---

**免责声明**: 本工具仅供学习和合法用途，请遵守校园网使用规定。