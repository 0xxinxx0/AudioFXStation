# AudioFX Station — UML диаграмма

Проект: **Avalonia (UI) + NAudio (аудио)**. Ядро архитектуры — интерфейс
`ISampleProvider`, все эффекты реализуют его через **паттерн Decorator**.

## 1. Диаграмма классов

```mermaid
classDiagram
    direction LR

    class ISampleProvider {
        <<interface>>
        +WaveFormat WaveFormat
        +Read(Span~float~ buffer) int
    }

    class Program {
        +Main(args) void$
        +BuildAvaloniaApp() AppBuilder$
    }
    class App {
        +Initialize() void
        +OnFrameworkInitializationCompleted() void
    }
    class MainWindow {
        -AudioFileReader audio
        -WaveOut output
        -EqualizerSampleProvider equalizer
        -CompressorSampleProvider compressor
        -ReverbSampleProvider reverb
        -VarispeedSampleProvider rateProvider
        -VolumeSampleProvider volumeProvider
        -SpectrumSampleProvider spectrumProvider
        -AudioSettings settings
        +OpenAudio_Click()
        +Play_Click() / Pause_Click() / Stop_Click()
        +Export_Click()
        +OpenEqualizer_Click() / OpenCompressor_Click() / OpenReverb_Click()
        -SaveSettings() / LoadSettings()
        -LoadWaveform() / DrawWaveform() / DrawSpectrum()
        -UpdateTimeline() / UpdateTrim() / SetSpeed()
    }
    class AudioSettings {
        +float SubGain / BassGain / MidGain / HighMidGain / TrebleGain
        +float Threshold / Ratio / Attack / Release / MakeupGain
    }

    class EqualizerSampleProvider {
        -ISampleProvider source
        -BiQuadFilter[][] filters
        +SetFiveGains(sub,bass,mid,highMid,treble)
        +SetGains(bass,mid,treble)
        +SetHighPass(enabled,freq)
        +Read(buffer) int
    }
    class CompressorSampleProvider {
        -ISampleProvider source
        +float Threshold/Ratio/Attack/Release/MakeupGain/GainReduction
        +SetSettings(threshold,ratio,attack,release,makeup)
        +Read(buffer) int
    }
    class ReverbSampleProvider {
        -ISampleProvider source
        -float[][] combBuffers/allPassBuffers
        +float Mix, Decay
        +SetMix(mix) / SetDecay(decay)
        +Read(buffer) int
    }
    class VarispeedSampleProvider {
        -ISampleProvider source
        +float Speed
        +Read(buffer) int
    }
    class VolumeSampleProvider {
        -ISampleProvider source
        +float Volume
        +SetVolume(volume)
        +Read(buffer) int
    }
    class TrimSampleProvider {
        -ISampleProvider source
        +Read(buffer) int
    }
    class SpectrumSampleProvider {
        -ISampleProvider source
        -Complex[] fft
        +Read(buffer) int
        -CalculateSpectrum()
    }

    class EqualizerWindow {
        -EqualizerSampleProvider equalizer
        -UpdateBands() / DrawGraph()
    }
    class CompressorWindow {
        -CompressorSampleProvider compressor
        -DispatcherTimer timer
        -UpdateSettings() / UpdateMeter()
    }
    class ReverbWindow {
        -ReverbSampleProvider reverb
        -SetMix(value)
    }

    ISampleProvider <|.. EqualizerSampleProvider
    ISampleProvider <|.. CompressorSampleProvider
    ISampleProvider <|.. ReverbSampleProvider
    ISampleProvider <|.. VarispeedSampleProvider
    ISampleProvider <|.. VolumeSampleProvider
    ISampleProvider <|.. TrimSampleProvider
    ISampleProvider <|.. SpectrumSampleProvider

    Program ..> App : builds
    App o-- MainWindow : creates
    MainWindow o-- EqualizerSampleProvider
    MainWindow o-- CompressorSampleProvider
    MainWindow o-- ReverbSampleProvider
    MainWindow o-- VarispeedSampleProvider
    MainWindow o-- VolumeSampleProvider
    MainWindow o-- SpectrumSampleProvider
    MainWindow o-- AudioSettings : persists
    MainWindow ..> EqualizerWindow : opens
    MainWindow ..> CompressorWindow : opens
    MainWindow ..> ReverbWindow : opens
    EqualizerWindow --> EqualizerSampleProvider : configures
    CompressorWindow --> CompressorSampleProvider : configures
    ReverbWindow --> ReverbSampleProvider : configures
```

## 2. Цепочка обработки аудио (Decorator)

```mermaid
flowchart LR
    A["AudioFileReader"] --> EQ["EqualizerSampleProvider"] --> CP["CompressorSampleProvider"]
    --> RV["ReverbSampleProvider"] --> VS["VarispeedSampleProvider"] --> VO["VolumeSampleProvider"]
    --> SP["SpectrumSampleProvider"] --> SW["SampleToWaveProvider"] --> OUT["WaveOut"]
    SP -. spectrumValues .-> SPEC["SpectrumCanvas"]
    A -. waveformPeaks .-> WAVE["WaveformCanvas"]
```

## 3. Последовательность: открытие файла и воспроизведение

```mermaid
sequenceDiagram
    autonumber
    actor U as Пользователь
    participant MW as MainWindow
    participant FD as StorageProvider
    participant AF as AudioFileReader
    participant CH as Цепочка ISampleProvider
    participant WO as WaveOut
    participant T as DispatcherTimer

    U->>MW: OpenAudio_Click()
    MW->>FD: OpenFilePickerAsync()
    FD-->>MW: путь файла
    MW->>AF: new AudioFileReader(path)
    MW->>CH: собрать цепочку провайдеров
    MW->>WO: Init(SampleToWaveProvider(spectrum))
    U->>MW: Play_Click()
    MW->>WO: Play()
    loop каждый буфер
      WO->>CH: Read(buffer)
      CH->>AF: Read()
      AF-->>CH: сэмплы
      CH-->>WO: обработанные сэмплы
    end
    T->>MW: Tick → UpdateTimeline()
```

## 4. Взаимодействие окон эффектов

```mermaid
flowchart LR
    MW["MainWindow"] -->|Show| EQW["EqualizerWindow"] -->|SetFiveGains| EQP["EqualizerSampleProvider"]
    MW -->|Show| CPW["CompressorWindow"] -->|SetSettings| CPP["CompressorSampleProvider"]
    MW -->|Show| RVW["ReverbWindow"] -->|SetMix| RVP["ReverbSampleProvider"]
    CPP -. GainReduction .-> CPW
```

## Ключевые связи
- **Program → App → MainWindow**: точка входа Avalonia создаёт главное окно.
- **MainWindow** — фасад: владеет всеми провайдерами, собирает цепочку, управляет `WaveOut`, хранит `AudioSettings` (JSON).
- **7 провайдеров реализуют `ISampleProvider`** и декорируют друг друга (Decorator).
- **TrimSampleProvider** реализует интерфейс, но в текущей цепочке `MainWindow`/`Export` не используется (заготовка).
- **Окна эффектов** получают ссылку на свой провайдер и настраивают его вживую.
