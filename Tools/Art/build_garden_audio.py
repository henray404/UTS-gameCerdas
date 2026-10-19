"""Quiet original synthesized ambience; never a gameplay hearing stimulus."""
import math, random, wave, struct
from pathlib import Path
root=(Path(__file__).resolve().parents[2]/'Assets/SneakHouse/Audio')
root.mkdir(parents=True,exist_ok=True)
rate=22050
def save(name,data):
    with wave.open(str(root/(name+'.wav')),'wb') as f:
        f.setnchannels(1);f.setsampwidth(2);f.setframerate(rate)
        f.writeframes(b''.join(struct.pack('<h',max(-32767,min(32767,int(x*32767)))) for x in data))
random.seed(90)
for kind in ('wind','water'):
    duration=18;n=rate*duration;low=0;slow=0;data=[]
    for i in range(n):
        t=i/rate;noise=random.uniform(-1,1)
        low+=.055*(noise-low);slow+=.003*(noise-slow)
        if kind=='wind': sample=(low*.35+slow*1.2)*(.65+.22*math.sin(t*.6981317))
        else: sample=low*.24+math.sin(2*math.pi*(430*t+8*math.sin(t*2.094395)))*.018*(.5+.5*math.sin(t*3.14159265))
        # Fade at loop seam to avoid a click.
        fade=min(1,t/.5,(duration-t)/.5);data.append(sample*fade)
    save('garden-'+kind,data)
data=[];duration=1.65;phase=0
for i in range(int(rate*duration)):
    t=i/rate;value=0
    for start,base in ((.12,2250),(.48,2700),(.88,2350)):
        u=t-start
        if 0<u<.18:
            frequency=base+900*math.sin(u/.18*math.pi)
            value+=math.sin(2*math.pi*(base*u+900*.18/math.pi*(1-math.cos(u/.18*math.pi))))*math.sin(u/.18*math.pi)**2*.20
    data.append(value)
save('garden-birds',data)
print('3 original ambient WAV clips written')
