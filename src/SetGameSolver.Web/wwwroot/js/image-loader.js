// Lädt ein Foto (aus Kamera oder Datei) oder ein Beispielbild (SVG), dreht Fotos gemäß EXIF, verkleinert
// und stellt die RGBA-Pixel für die Erkennung in .NET bereit. Alles bleibt im Browser.

let lastPixels = null;

async function decode(blob, maxSide) {
    const bitmap = await createImageBitmap(blob, { imageOrientation: 'from-image' });
    try {
        return draw(bitmap, bitmap.width, bitmap.height, maxSide);
    } finally {
        bitmap.close?.();
    }
}

function draw(source, sourceWidth, sourceHeight, maxSide) {
    const scale = Math.min(1, maxSide / Math.max(sourceWidth, sourceHeight));
    const width = Math.max(1, Math.round(sourceWidth * scale));
    const height = Math.max(1, Math.round(sourceHeight * scale));

    const canvas = document.createElement('canvas');
    canvas.width = width;
    canvas.height = height;
    const ctx = canvas.getContext('2d', { willReadFrequently: true });
    ctx.imageSmoothingQuality = 'high';
    ctx.drawImage(source, 0, 0, width, height);

    lastPixels = new Uint8Array(ctx.getImageData(0, 0, width, height).data.buffer);
    const url = canvas.toDataURL('image/jpeg', 0.85);
    return { width, height, url };
}

export async function loadFromInput(input, maxSide) {
    const file = input?.files?.[0];
    if (!file) return null;
    try {
        return await decode(file, maxSide);
    } finally {
        input.value = ''; // dasselbe Foto erneut wählbar machen
    }
}

// Zeichnet ein SVG (Beispielbild aus .NET) und stellt es wie ein Foto bereit.
export async function loadFromSvg(svg, maxSide) {
    const url = URL.createObjectURL(new Blob([svg], { type: 'image/svg+xml' }));
    try {
        const image = new Image();
        image.src = url;
        await image.decode();
        return draw(image, image.naturalWidth, image.naturalHeight, maxSide);
    } finally {
        URL.revokeObjectURL(url);
    }
}

// Gibt die Pixel des zuletzt geladenen Bildes zurück (als byte[] in .NET).
export function takePixels() {
    const pixels = lastPixels;
    lastPixels = null;
    return pixels;
}

// Kurz dem Browser Zeit geben, die Oberfläche neu zu zeichnen, bevor gerechnet wird.
export function nextFrame() {
    return new Promise(resolve => requestAnimationFrame(() => setTimeout(resolve, 0)));
}
