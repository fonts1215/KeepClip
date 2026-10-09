# Generate a clear 180 ms chime with smooth attack/release. Never plays the sound.
param([string]$Output = (Join-Path $PSScriptRoot '../src/KeepClip.App/Assets/Copy.wav'))
$amplitude = 0.56 # Four times the previous amplitude (+12 dB), with headroom to avoid clipping.
$rate = 44100; $duration = 0.18; $count = [int]($rate * $duration)
$stream = [IO.File]::Create([IO.Path]::GetFullPath($Output))
$writer = [IO.BinaryWriter]::new($stream)
try {
    $writer.Write([Text.Encoding]::ASCII.GetBytes('RIFF')); $writer.Write([int](36 + 2 * $count))
    $writer.Write([Text.Encoding]::ASCII.GetBytes('WAVEfmt ')); $writer.Write([int]16)
    $writer.Write([int16]1); $writer.Write([int16]1); $writer.Write([int]$rate)
    $writer.Write([int]($rate * 2)); $writer.Write([int16]2); $writer.Write([int16]16)
    $writer.Write([Text.Encoding]::ASCII.GetBytes('data')); $writer.Write([int]($count * 2))
    for ($i = 0; $i -lt $count; $i++) {
        $t = $i / $rate; $phase = $i / ($count - 1)
        $envelope = [Math]::Pow([Math]::Sin([Math]::PI * $phase), 1.8) * [Math]::Exp(-1.6 * $phase)
        $tone = 0.75 * [Math]::Sin(2 * [Math]::PI * 587.33 * $t) + 0.25 * [Math]::Sin(2 * [Math]::PI * 880 * $t)
        $writer.Write([int16]([Math]::Round(32767 * $amplitude * $envelope * $tone)))
    }
} finally { $writer.Dispose(); $stream.Dispose() }
