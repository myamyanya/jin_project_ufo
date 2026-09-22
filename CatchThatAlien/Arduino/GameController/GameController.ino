const int pinButton = A0;
const int pinSound = A1;
const int pinVibration = A2;
const int pinLight = A3;
const int pinFSR1 = A4;
const int pinFSR2 = A5;

unsigned long vibrationEndTime = 0;
bool isVibrating = false;

// 设置单次震动的时长 (毫秒)
const int VIBRATION_DURATION = 200; 

void setup() {
  // 与 Unity 通信的波特率，需与 Unity 脚本中保持一致
  Serial.begin(9600); 
  
  // 按钮一般用 INPUT 或 INPUT_PULLUP
  // 如果你的按钮模块自带上拉电阻，可以用 INPUT
  pinMode(pinButton, INPUT_PULLUP); 
  
  // 震动马达
  pinMode(pinVibration, OUTPUT);
  digitalWrite(pinVibration, LOW);
}

void loop() {
  // 1. 接收来自 Unity 的指令
  if (Serial.available() > 0) {
    char cmd = Serial.read();
    
    // 如果收到 'V'，触发一次震动
    if (cmd == 'V') {
      isVibrating = true;
      digitalWrite(pinVibration, HIGH);
      
      // 记录结束震动的时间
      vibrationEndTime = millis() + VIBRATION_DURATION; 
    }
  }

  // 2. 检查震动是否该停止了 (非阻塞等待)
  if (isVibrating && millis() > vibrationEndTime) {
    isVibrating = false;
    digitalWrite(pinVibration, LOW);
  }

  // 3. 读取所有传感器的数据
  int btn = digitalRead(pinButton); // 用 digitalRead 确保按钮返回 0 或 1，避免 analog 产生的浮动值
  // 音量传感器改回 Aout，用 analogRead 读取波形
  int sound = analogRead(pinSound); 
  int light = analogRead(pinLight);
  int fsr1 = analogRead(pinFSR1);
  int fsr2 = analogRead(pinFSR2);

  // 4. 将数据以逗号分隔的格式发送给 Unity
  // 格式: btn,sound,light,fsr1,fsr2
  Serial.print(btn);   Serial.print(",");
  Serial.print(sound); Serial.print(",");
  Serial.print(light); Serial.print(",");
  Serial.print(fsr1);  Serial.print(",");
  Serial.print(fsr2);
  Serial.println();    // 换行，Unity 根据换行来读取一帧数据

  // 稍微延迟一下，防止发得太快把串口卡死（约 50 次/秒）
  delay(20); 
}
