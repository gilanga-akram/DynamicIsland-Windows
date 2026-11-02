import fs from 'fs';

const text = fs.readFileSync('C:/Users/PC/.gemini/antigravity/scratch/dynamic-island/pin.html', 'utf8');
const regex = /https:\/\/i\.pinimg\.com\/[^\s"'<>]+\.(?:gif|mp4|webm|jpg|jpeg|png)/g;
const matches = new Set(text.match(regex) || []);
for (const m of matches) {
    console.log(m);
}
