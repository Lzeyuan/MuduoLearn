#!/bin/bash

TARGET_IP="192.168.8.20"
OUTPUT_FILE_PREFIX="ttcp_output"

# 定义一个数组
countList=(1024 2048 4096 8192 16384 32768 65536 131072)
sizeList=(8 16 32 64 128 256 512 1024 2048 4096 8192 16384)

# 创建文件夹
FILE_OUTPUT_DIR="ttcp_test"
if [ ! -d "$FILE_OUTPUT_DIR" ]; then
    mkdir "$FILE_OUTPUT_DIR"
fi

# 创建文件或者新开一行
startDate=$(date "+%Y-%m-%d")
outFileName="${FILE_OUTPUT_DIR}/ttcp_send_output_${startDate}.txt"
echo " " >>"$outFileName"
startTime=$(date "+%Y-%m-%d %H:%M:%S")
echo "$startTime" >>"$outFileName"

for size in "${sizeList[@]}"; do
    row="${size}"
    for count in "${countList[@]}"; do
        # 输出时间
        startTime=$(date "+%Y-%m-%d %H:%M:%S")
        echo $startTime

        # 执行输出到终端
        ttcpOutput=$(dotnet run -- start -t "$TARGET_IP" -c "$count" -s "$size")
        echo "$ttcpOutput"

        # 添加信息到行
        ttcpOutput2File=$(echo "$ttcpOutput" | tail -n 1 | awk '{print$1}')
        row="${row},${ttcpOutput2File}"
        sleep 0.1
        echo ""
    done
    # 保持一行信息
    echo "$row" >>"$outFileName"
done

echo "All ttcp commands have been executed. Output is in $OUTPUT_FILE."
