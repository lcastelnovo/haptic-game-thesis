using UnityEngine;

namespace HapticResearch.Labyrinth
{
    // Parte comune dei due cue: la SOSTA.
    //
    // La sosta non e' un ritardo da nascondere, e' il battito di gioco: l'attuatore
    // Peltier impiega 2-3 s a raggiungere il valore, quindi la lettura non puo' essere
    // istantanea. Invece di far indovinare al partecipante quando e' pronta, un tono che
    // sale di altezza gli dice quanto manca (stessa meccanica dell'hold di Level 1) e un
    // colpetto segna la fine.
    //
    // Sta nella base, non nelle sottoclassi, perche' il timing DEVE essere identico fra
    // condizione termica e condizione audio: e' l'unica cosa che rende il confronto equo.
    public abstract class JunctionCueBase : IJunctionCue
    {
        protected readonly HapticProfile Profile;

        private readonly AudioSource toneSource;
        private readonly AudioSource sfxSource;
        private readonly AudioClip dwellTone;
        private readonly AudioClip readyTick;
        private readonly float sfxVolume;

        private bool onCorrectBranch;
        private float held;
        private bool leaving;
        private float leftFor;

        public abstract string CueName { get; }

        public bool Armed { get; private set; }
        public bool Reading { get; private set; }
        public bool ReadingReady { get; private set; }
        public int ReadingsCompleted { get; private set; }
        public int JunctionIndex { get; private set; } = -1;

        // Su quale ramo e' la lettura in corso (valido solo mentre Reading).
        public bool ReadingCorrectBranch => onCorrectBranch;

        public float Progress01 =>
            Profile == null || Profile.ThermalDwellSeconds <= 0f ? 1f
            : Mathf.Clamp01(held / Profile.ThermalDwellSeconds);

        protected JunctionCueBase(HapticProfile profile, AudioSource toneSource, AudioSource sfxSource,
                                  AudioClip dwellTone, AudioClip readyTick, float sfxVolume)
        {
            Profile = profile;
            this.toneSource = toneSource;
            this.sfxSource = sfxSource;
            this.dwellTone = dwellTone;
            this.readyTick = readyTick;
            this.sfxVolume = sfxVolume;
        }

        public void Arm(int junctionIndex)
        {
            AbortReading();
            Armed = true;
            JunctionIndex = junctionIndex;
            ReadingsCompleted = 0;
            OnArmed(junctionIndex);
        }

        public void Disarm()
        {
            AbortReading();
            Armed = false;
            JunctionIndex = -1;
            OnDisarmed();
        }

        public void BeginReading(bool correctBranch)
        {
            if (!Armed) return;

            // Stesso ramo di prima: si stava solo tremando fuori dal bordo, la sosta continua.
            if (Reading && correctBranch == onCorrectBranch) { leaving = false; leftFor = 0f; return; }

            AbortReading();
            Reading = true;
            ReadingReady = false;
            onCorrectBranch = correctBranch;
            held = 0f;
            StartTone();
            OnReadingStarted(correctBranch);
        }

        // Il dito ha lasciato la piastrella. Non si annulla subito: un tremolio della mano
        // non deve buttare via due secondi di sosta.
        public void CancelReading()
        {
            if (!Reading) return;
            leaving = true;
        }

        public void Tick(float deltaTime)
        {
            if (!Reading) return;

            if (leaving)
            {
                leftFor += deltaTime;
                float grace = Profile != null ? Profile.DwellGraceSeconds : 0.25f;
                if (leftFor >= grace) { AbortReading(); return; }
            }

            if (ReadingReady) return;

            held += deltaTime;
            if (toneSource != null && dwellTone != null)
                toneSource.pitch = Mathf.Lerp(1f, 2f, Progress01);

            float dwell = Profile != null ? Profile.ThermalDwellSeconds : 2.5f;
            if (held < dwell) return;

            ReadingReady = true;
            ReadingsCompleted++;
            StopTone();
            if (sfxSource != null && readyTick != null) sfxSource.PlayOneShot(readyTick, sfxVolume);
            OnReadingReady(onCorrectBranch);
        }

        private void AbortReading()
        {
            bool was = Reading;
            Reading = false;
            ReadingReady = false;
            held = 0f;
            leaving = false;
            leftFor = 0f;
            StopTone();
            if (was) OnReadingCancelled();
        }

        private void StartTone()
        {
            if (toneSource == null || dwellTone == null) return;
            toneSource.clip = dwellTone;
            toneSource.pitch = 1f;
            if (!toneSource.isPlaying) toneSource.Play();
        }

        private void StopTone()
        {
            if (toneSource == null) return;
            toneSource.pitch = 1f;
            if (toneSource.isPlaying) toneSource.Stop();
        }

        protected void PlaySfx(AudioClip clip)
        {
            if (sfxSource != null && clip != null) sfxSource.PlayOneShot(clip, sfxVolume);
        }

        // --- da specializzare ---------------------------------------------------------

        protected virtual void OnArmed(int junctionIndex) { }
        protected virtual void OnDisarmed() { }
        protected virtual void OnReadingStarted(bool correctBranch) { }
        protected virtual void OnReadingReady(bool correctBranch) { }
        protected virtual void OnReadingCancelled() { }
    }
}
