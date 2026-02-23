"""
序列帧PNG → VP8 WebM (带Alpha透明通道) 转换工具
注意: Unity不支持VP9，必须用VP8编码 (libvpx)

用法:
  python frames_to_webm.py <序列帧目录> <输出webm路径> [fps] [crf]

示例:
  python frames_to_webm.py ./frames/tier1 ./output/tier1-sp.webm 24 18

  批量转换所有tier:
  python frames_to_webm.py --batch ./frames ./output 24 18

参数:
  序列帧目录: 包含 frame_000.png, frame_001.png, ... 的目录
  输出路径:   输出的.webm文件路径
  fps:        帧率，默认24
  crf:        质量(0-63, 越小越好)，默认18

要求:
  - ffmpeg (支持VP8编码 + libvpx)
  - 序列帧PNG必须有Alpha通道(RGBA)
"""

import subprocess
import sys
import os
import glob

# ffmpeg路径（Windows WinGet安装位置）
FFMPEG = r"C:\Users\Administrator\AppData\Local\Microsoft\WinGet\Packages\Gyan.FFmpeg_Microsoft.Winget.Source_8wekyb3d8bbwe\ffmpeg-8.0.1-full_build\bin\ffmpeg.exe"

def check_ffmpeg():
    """检查ffmpeg是否可用"""
    if not os.path.exists(FFMPEG):
        print(f"ERROR: ffmpeg not found at {FFMPEG}")
        print("Please install ffmpeg or update the FFMPEG path")
        return False
    try:
        result = subprocess.run([FFMPEG, "-version"], capture_output=True, text=True, timeout=10)
        ver = result.stdout.split('\n')[0] if result.stdout else "unknown"
        print(f"ffmpeg: {ver}")
        return True
    except Exception as e:
        print(f"ERROR checking ffmpeg: {e}")
        return False

def count_frames(frames_dir):
    """统计序列帧数量"""
    patterns = ["frame_*.png", "frame_*.PNG", "*.png", "*.PNG"]
    for pat in patterns:
        files = sorted(glob.glob(os.path.join(frames_dir, pat)))
        if files:
            return files, pat
    return [], None

def frames_to_webm(frames_dir, output_path, fps=24, crf=18):
    """
    将序列帧PNG转换为VP9 WebM（带Alpha透明通道）

    VP9+Alpha原理：
    - VP9原生支持Alpha通道（yuva420p像素格式）
    - ffmpeg用 -pix_fmt yuva420p 启用Alpha编码
    - 输出的WebM文件在Unity VideoPlayer中可直接播放透明视频
    """
    # 检查输入目录
    if not os.path.isdir(frames_dir):
        print(f"ERROR: Directory not found: {frames_dir}")
        return False

    files, pattern = count_frames(frames_dir)
    if not files:
        print(f"ERROR: No PNG frames found in {frames_dir}")
        return False

    print(f"Found {len(files)} frames in {frames_dir} (pattern: {pattern})")
    print(f"First: {os.path.basename(files[0])}, Last: {os.path.basename(files[-1])}")

    # 确保输出目录存在
    output_dir = os.path.dirname(output_path)
    if output_dir and not os.path.exists(output_dir):
        os.makedirs(output_dir, exist_ok=True)

    # 判断文件名模式（支持 frame_%03d.png 或用glob输入）
    # 尝试检测命名模式
    first_file = os.path.basename(files[0])
    if first_file.startswith("frame_"):
        # frame_000.png 模式 → 用 frame_%03d.png
        # 检测数字位数
        num_part = first_file.replace("frame_", "").replace(".png", "").replace(".PNG", "")
        num_digits = len(num_part)
        input_pattern = os.path.join(frames_dir, f"frame_%0{num_digits}d.png")
    else:
        # 其他命名 → 用glob + concat方式
        input_pattern = None

    # 构建ffmpeg命令
    # VP8 + Alpha 编码参数（Unity兼容）:
    # -c:v libvpx         VP8编码器（Unity不支持VP9！）
    # -pix_fmt yuva420p   启用Alpha通道（关键！）
    # -b:v 1M             目标码率
    # -auto-alt-ref 0     VP8+Alpha必须关闭alt-ref
    # -an                 无音频

    if input_pattern:
        cmd = [
            FFMPEG,
            "-y",                       # 覆盖输出
            "-framerate", str(fps),     # 输入帧率
            "-i", input_pattern,        # 输入模式
            "-c:v", "libvpx",          # VP8编码（Unity兼容）
            "-pix_fmt", "yuva420p",     # Alpha通道！
            "-b:v", "1M",             # 目标码率
            "-auto-alt-ref", "0",      # VP8+Alpha必须
            "-an",                     # 无音频
            output_path
        ]
    else:
        # 用concat demuxer处理非标准命名
        concat_file = os.path.join(frames_dir, "_concat_list.txt")
        with open(concat_file, 'w') as f:
            for fpath in files:
                f.write(f"file '{os.path.abspath(fpath)}'\n")
                f.write(f"duration {1.0/fps}\n")

        cmd = [
            FFMPEG,
            "-y",
            "-f", "concat",
            "-safe", "0",
            "-i", concat_file,
            "-c:v", "libvpx",
            "-pix_fmt", "yuva420p",
            "-b:v", "1M",
            "-auto-alt-ref", "0",
            "-an",
            output_path
        ]

    print(f"\nEncoding VP9+Alpha WebM...")
    print(f"  FPS: {fps}")
    print(f"  CRF: {crf} (lower=better, 0=lossless)")
    print(f"  Output: {output_path}")
    print(f"  Command: {' '.join(cmd)}\n")

    try:
        result = subprocess.run(cmd, capture_output=True, text=True, timeout=300)

        if result.returncode != 0:
            print(f"ERROR: ffmpeg failed (exit code {result.returncode})")
            print(f"STDERR:\n{result.stderr[-2000:]}")
            return False

        # 检查输出
        if os.path.exists(output_path):
            size = os.path.getsize(output_path)
            size_str = f"{size/1024:.1f}KB" if size < 1024*1024 else f"{size/1024/1024:.1f}MB"
            duration = len(files) / fps
            print(f"SUCCESS: {output_path}")
            print(f"  Size: {size_str}")
            print(f"  Duration: {duration:.2f}s ({len(files)} frames @ {fps}fps)")
            return True
        else:
            print(f"ERROR: Output file not created")
            return False

    except subprocess.TimeoutExpired:
        print("ERROR: ffmpeg timed out (>300s)")
        return False
    except Exception as e:
        print(f"ERROR: {e}")
        return False

def batch_convert(frames_root, output_dir, fps=24, crf=18):
    """
    批量转换: frames_root/tier1/ → output_dir/tier1-sp.webm
    """
    print(f"=== Batch Convert: {frames_root} → {output_dir} ===\n")

    success = 0
    failed = 0

    for tier in range(1, 7):
        tier_dir = os.path.join(frames_root, f"tier{tier}")
        if not os.path.isdir(tier_dir):
            print(f"SKIP: tier{tier} directory not found")
            continue

        files, _ = count_frames(tier_dir)
        if len(files) <= 1:
            print(f"SKIP: tier{tier} has only {len(files)} frame(s)")
            continue

        output_path = os.path.join(output_dir, f"tier{tier}-sp.webm")
        print(f"\n--- Tier {tier}: {len(files)} frames ---")

        if frames_to_webm(tier_dir, output_path, fps, crf):
            success += 1
        else:
            failed += 1

    print(f"\n=== Batch Complete: {success} success, {failed} failed ===")
    return failed == 0

if __name__ == "__main__":
    if not check_ffmpeg():
        sys.exit(1)

    if len(sys.argv) < 2:
        print(__doc__)
        sys.exit(0)

    if sys.argv[1] == "--batch":
        # 批量模式: python frames_to_webm.py --batch <frames_root> <output_dir> [fps] [crf]
        if len(sys.argv) < 4:
            print("Usage: python frames_to_webm.py --batch <frames_root> <output_dir> [fps] [crf]")
            sys.exit(1)

        frames_root = sys.argv[2]
        output_dir = sys.argv[3]
        fps = int(sys.argv[4]) if len(sys.argv) > 4 else 24
        crf = int(sys.argv[5]) if len(sys.argv) > 5 else 18

        ok = batch_convert(frames_root, output_dir, fps, crf)
        sys.exit(0 if ok else 1)
    else:
        # 单个模式: python frames_to_webm.py <frames_dir> <output.webm> [fps] [crf]
        if len(sys.argv) < 3:
            print("Usage: python frames_to_webm.py <frames_dir> <output.webm> [fps] [crf]")
            sys.exit(1)

        frames_dir = sys.argv[1]
        output_path = sys.argv[2]
        fps = int(sys.argv[3]) if len(sys.argv) > 3 else 24
        crf = int(sys.argv[4]) if len(sys.argv) > 4 else 18

        ok = frames_to_webm(frames_dir, output_path, fps, crf)
        sys.exit(0 if ok else 1)
