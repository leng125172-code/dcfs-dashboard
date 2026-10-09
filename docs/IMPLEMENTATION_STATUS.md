# 实施状态与恢复入口

截至 2026-10-09（Asia/Shanghai），完整后端计划仍在实施中。构建通过不等于所有业务与远程验收完成。

## 工作站恢复后的只读检查

工作站离线期间暂停了 SSH、部署、重启和清理。用户确认重新连接后，2026-10-09 18:25（Asia/Shanghai）的只读检查确认：8 个基础容器仍健康、旧容器停止、SHA256SUMS 校验通过、两个 IP 在线，磁盘容量充足。远程配置仓库仍为 `06e0e94`，未提交的 `.migration-state/` 属于迁移状态，不删除或提交。

为无人值守验收，SSH 用户当前临时启用了 `NOPASSWD:ALL`。该规则只作为本轮实施期间的临时入口，完成需要 root 的验收后必须删除 `/etc/sudoers.d/whaledeck-temporary-user` 并重新执行 `visudo -cf /etc/sudoers`；长期保留的 Agent 权限仍仅允许调用固定 helper。用户提供的密码没有写入脚本、仓库、日志或命令输出。

本轮恢复后的实际进展：

- `database-platform` 已同步到 `4fb26dd`；Whale Deck `dev` 源码已克隆到 `/data/GitRepos/whale-deck`。
- `scripts/check-all.sh` 全部通过，数据库、缓存及 Authentik 内部健康和端口边界符合检查规则。
- 已收紧并持久化 Whale Deck 专用 Valkey ACL：清除旧授权、排除危险命令，防止 `FLUSHALL/FLUSHDB` 等无键命令绕过键前缀限制。
- `validation/check-whaledeck-credentials.sh` 在真实依赖上通过：PostgreSQL 专用身份和最小角色权限、Valkey 专用认证与允许/拒绝规则。验证使用只读 SQL 和 `ACL DRYRUN`，不执行危险命令、不写入业务数据。
- 已完成 Linux x64 自包含发布与宿主安装。Agent 和 MaintenanceHost 均由 systemd 管理、开机自启，安装升级脚本使用替换 inode 的方式覆盖运行中二进制，避免 `ETXTBSY`。
- 已完成 Agent UDS、固定 helper、双网卡维护入口、文件权限和 Docker 访问验收；Agent 不监听 TCP，API/Worker 也不需要挂载 `docker.sock`。
- 已执行受控 Docker 重启验收：Agent 与 MaintenanceHost 进程未重启，8 个基础容器重启后全部恢复健康，数据库平台完整检查通过。
- 已将 Docker 默认日志切换为有界 `local` 驱动（10 MiB、5 个文件、压缩），启用 `live-restore`，并保留轩辕镜像加速配置。
- 工作站现有 Docker Buildx、BuildKit 与 Compose 已验证可用，支持 `linux/amd64`；没有创建多余的 BuildKit 容器，也没有构建 Whale Deck 镜像。

更新 timer 已完成切换：`database-platform-workstation-update.timer` 为 enabled/active，旧 timer 为 disabled/inactive，下一次计划执行时间为 2026-10-11 20:00 CST。

2026-10-09 20:13（Asia/Shanghai）后续状态：Agent 与 MaintenanceHost 已作为 systemd 服务安装并启动，均 enabled/active、`NRestarts=0`。后续已修复 Authentik 端口上的维护状态路由优先级、离线页 YARP Warning、运行中二进制升级和 UDS 启动竞态；宿主验证脚本与 Docker 重启韧性脚本均通过。

离线前最后一次成功记录：

- 8 个 `database-platform-*` 基础容器健康，Whale Deck PostgreSQL 数据库/角色及 Valkey ACL 已初始化。
- 旧容器保持停止状态，旧网络保留；未执行最终删除。
- 安全备份：`/data/DockerData/migration-dcfs-20261009_163559`。
- 保留 `/data/GitRepos/DCFS -> /data/GitRepos/database-platform` 兼容链接用于旧容器配置挂载回滚。仅在完成验收和旧容器清理后移除此链接。
- Agent、MaintenanceHost 和新名称更新 timer 已完成安装验收。
- 本次未构建、启动或推送 Whale Deck 容器镜像。

## 当前本地实现

- Contracts、Agent、MaintenanceHost、Application、Infrastructure、API、Worker 工程及 EF 初始 migration。
- Swagger UI / 内置 OpenAPI、门户、基础查询、容器生命周期、任务/Outbox 骨架与权限快照。
- 七阶段安装器及纯预览、准备、依赖安装入口；一次性 Migrator；独立宿主服务发布目录。
- 配置复用、加速源合并/验证/回滚、Git origin/脏工作区检查、安装互斥锁、镜像摘要部署检查。

## 本地验证记录

- .NET Release 构建通过：0 警告、0 错误。
- .NET 自动化测试 38 项通过（29 项 UnitTests、9 项 IntegrationTests；其中身份测试使用离线 HTTP/cache 替身）。
- 安装器命令行 5 项检查通过，空 PATH 下验证 dry-run 不调用系统工具。
- 依赖环境生成测试通过：专用密码独立生成，重复执行不覆盖原文件。
- Shell 语法检查通过；已完成宿主组件正式安装与工作站验收，但未执行 API/Worker 镜像构建。

## 仍需实现或实测

Worker 已能在重启后根据持久化的 Agent operation id 恢复轮询，持续同步进度和终态，并把取消请求传播到 Agent；未知的未来 Agent 状态按等待处理，避免错误地报告成功。Worker 向 Agent 传递 job id 与幂等键，Agent 以 SHA-256 索引将请求绑定到持久化 operation；API/Worker 或 Agent 在响应前重启时，重试返回原 operation，不会重复执行同一容器动作。下列项目仍不能标记为完成：六类数据库管理适配器与真实 CRUD/授权/轮换验收，应用安装/更新/回滚执行器，备份与定时调度，指标日分区/聚合，告警和配置推送，完整 Authentik 用户与 SSO 写操作，Docker 配置维护的业务端到端回滚。

资源写操作现已增加 PostgreSQL 持久租约：同一逻辑资源一次只允许一个 job 持有，Worker 每 6 秒续租，进程失联 30 秒后可由其他实例接管，终态主动释放。初始 migration 与资源租约 migration 已在工作站的空 `whaledeck` 数据库中由独立 Migrator 成功应用，共创建 27 张表；API/Worker 仍未部署或启动。Agent 幂等版本已原位升级并再次通过宿主验证。

一次性 Secret 通道已实现：随机 256 位令牌放在 POST body，Secret 按 Authentik subject 隔离，5 分钟 TTL，使用 Valkey `GETDEL` 原子消费且不进入 URL、PostgreSQL、审计或日志；开发环境使用同语义的内存实现。工作站 Valkey ACL 已确认允许专用键空间上的 `GETDEL`。该通道尚未接入仍待实现的数据库账号创建/轮换适配器。

Agent capability 已限制为最长 60 秒、调用方法签名绑定和一次性 nonce；签发端使用 45 秒有效期，并增加过期、错方法、超长有效期及重放测试。Unix socket 同时校验 `SO_PEERCRED`：只接受 root 或安装时记录的 Whale Deck 主组，不能仅凭补充组和 socket 文件权限绕过进程身份检查。

宿主服务安装/升级、systemd 沙箱、受限 sudo helper、UDS 与 Docker 重启韧性已在目标 Linux 工作站实测通过。Authentik 的 OIDC subject 与管理 API 用户 ID 映射仍需完善；不能据宿主验收结果认定生产认证已完成。

部分管理端点与 RPC 目前只是受控入队或注册信息查询，不能当作上述业务已可执行。现有测试包括离线替身测试，不代表 PostgreSQL、Valkey、Docker 或 Authentik 真实依赖集成测试已通过。

## 后续顺序

1. 补齐异步任务执行、租约、资源锁、Agent 操作轮询与重启恢复。
2. 实现六类数据库管理适配器、备份/调度和临时资源真实集成验收。
3. 实现应用安装、更新、回滚执行器和其余 P1 写操作。
4. 完成 Authentik 管理写操作、指标分区/聚合、告警与配置推送。
5. 完整业务验收通过后清理旧容器对象/旧网络及兼容链接，保留所有 NVMe/HDD 数据。
6. 删除临时全量 sudo 规则并复验 Agent 固定 helper 的最小权限路径。

API/Worker 不临时安装成 systemd 服务。生成正式镜像与正式部署在安装脚本中定义，当前开发任务不执行镜像阶段。
