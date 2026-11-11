# Rurimecha FFmpeg 构建

`9.0.2-rurimecha.1` 包含独立运行的 `ffmpeg` 和 `ffprobe`。三个目标共用同一份源码，Windows 和 Linux arm64 通过交叉编译生成。

| 目标 | 打包用途 | 运行要求 |
| --- | --- | --- |
| `linux-amd64` | Linux x86-64 和 Docker linux/amd64 | glibc 2.35 或更新版本 |
| `linux-arm64` | Linux AArch64 和 Docker linux/arm64 | glibc 2.35 或更新版本 |
| `windows-amd64` | Windows x64 安装包 | 64 位 Windows |

## 固定源码与构建配置

`sources/` 含上游源码归档。[SHA256SUMS](sources/SHA256SUMS) 固定了每个归档的内容。

| 组件 | 固定版本及来源 |
| --- | --- |
| FFmpeg | 9.0.2 分支提交 [`d4070a14cecc7e29eb278f9b6036c2284255f70d`](https://github.com/FFmpeg/FFmpeg/tree/d4070a14cecc7e29eb278f9b6036c2284255f70d) |
| x264 | VideoLAN x264 提交 [`0480cb05fa188d37ae87e8f4fd8f1aea3711f7ee`](https://github.com/mirror/x264/tree/0480cb05fa188d37ae87e8f4fd8f1aea3711f7ee) |
| LAME | [3.100](https://sourceforge.net/projects/lame/files/lame/3.100/) |
| Opus | [1.6.1](https://downloads.xiph.org/releases/opus/opus-1.6.1.tar.gz) |
| libogg | [1.3.6](https://downloads.xiph.org/releases/ogg/libogg-1.3.6.tar.gz) |
| libvorbis | [1.3.7](https://downloads.xiph.org/releases/vorbis/libvorbis-1.3.7.tar.gz) |
| zlib | [1.3.2](https://zlib.net/fossils/zlib-1.3.2.tar.gz) |

[Dockerfile](Dockerfile) 固定 Ubuntu 构建基础镜像，[build.sh](build.sh) 使用 GCC、AArch64 GCC 和 MinGW-w64 对应工具链。脚本在解压前校验归档，并静态链接上述媒体库。FFmpeg 启用 GPL 与 version3，提供 H.264、MP3、Opus、Vorbis 和内置 AAC 编码、缩放和响度处理、`fd` 可寻址输入与字幕、图片处理。每份产物附构建配置、实际工具链软件包版本和二进制摘要。系统工具链的软件包由容器发行版维护。

`Dockerfile`、`build.sh`、`smoke.py` 及对应的 GitHub Actions 构建配置按 [MIT](LICENSE.build) 授权。各上游源码沿用各自的许可证。合成的 FFmpeg 工具按 GPL-3.0-or-later 分发，随包携带 [许可文本](licenses) 和 [对应源码指引](SOURCE.txt)。

## 在 GitHub Actions 构建

在 **Actions → Build FFmpeg tools → Run workflow** 手动启动，工作流需要存在于仓库默认分支。`reuse_run_id` 默认选择已验证成功的运行 `42772332252`，直接复用其中的 Linux amd64、Windows amd64 和对应源码附件。需要更换构建时可填写本仓库同一工作流的其他成功运行 ID；只有清空该输入才会重新编译三个目标。[GitHub 托管 runner](https://docs.github.com/en/actions/reference/runners/github-hosted-runners)

在私有分发仓库运行时，工作流会核对源码归档中的 Git 提交，计算两个 amd64 工具包和源码包的校验和，再一起保存到 `ghcr.io/rurimecha/ffmpeg-tools`。给 Core 仓库授予该包的 Actions 读取权限，并将运行摘要中的 `sha256:…` 保存到 Core 仓库变量 `RURI_FFMPEG_DIGEST`。此后候选版打包按摘要下载，升级工具时才需要更新该变量。[GHCR 跨仓库读取权限](https://docs.github.com/en/packages/learn-github-packages/configuring-a-packages-access-control-and-visibility#ensuring-workflow-access-to-your-package)

## 在已有 Docker 的机器上构建

从仓库根目录运行以下命令。把 `platform` 换成所需目标。编译器由构建容器提供。

```sh
platform=linux-amd64
docker buildx build --platform linux/amd64 \
  --build-arg "TARGET=$platform" \
  --output "type=local,dest=releases/ffmpeg/9.0.2-rurimecha.1/$platform" \
  third_party/ffmpeg
```

在目标系统上执行验收。

```sh
python3 third_party/ffmpeg/smoke.py linux-amd64 releases/ffmpeg/9.0.2-rurimecha.1/linux-amd64
```

## 作为发行输入使用

Rurimecha 打包脚本接收同时包含两个 amd64 工具归档、对应源码归档和 `SHA256SUMS` 的目录。它会校验归档并保留工具的许可文件、源码说明和源码校验和，同时附上 [IJG 声明](redistribution/IJG.txt) 和 Windows 构建使用的 [MinGW-w64 与 Winpthreads 8.0.0 声明](redistribution/windows-amd64)。Docker 选与镜像架构一致的 Linux 工具，基础镜像须有兼容的 glibc。

公开发行时，在二进制下载入口同时提供 `ruri-ffmpeg-9.0.2-rurimecha.1-sources.tar.gz`。发布容器镜像时，在镜像的说明页明确链接到对应发行版的源码。 [GNU GPLv3 第 6 节](https://www.gnu.org/licenses/gpl-3.0.html#section6)

## 本地验证记录

`9.0.2-rurimecha.1` 在 Linux amd64 和 Windows x64 原生运行了 Ruri 播放回归，覆盖音频格式、响度标准化、内嵌字幕与封面、缓存和珍藏片段。Linux arm64 在 QEMU 中实跑了探测、跳转、三种音频编码、预览和字幕提取。两种 Linux 构建的最高 glibc 符号版本为 2.35。
