import math,random,struct,wave
from pathlib import Path
out=(Path(__file__).resolve().parents[2]/'Assets/SneakHouse/Audio/can-impact.wav')
random.seed(17);rate=22050;data=[]
for i in range(int(rate*.55)):
 t=i/rate
 value=sum(math.sin(2*math.pi*f*t)*math.exp(-t*d)*a for f,d,a in [(530,17,.3),(1097,23,.23),(1733,29,.17),(2821,37,.10)])
 value+=random.uniform(-1,1)*math.exp(-t*90)*.20
 data.append(struct.pack('<h',int(max(-1,min(1,value))*32767)))
with wave.open(str(out),'wb') as f:f.setnchannels(1);f.setsampwidth(2);f.setframerate(rate);f.writeframes(b''.join(data))
print('Original metallic impact WAV written')
