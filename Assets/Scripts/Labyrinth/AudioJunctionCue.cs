using UnityEngine;

namespace HapticResearch.Labyrinth
{
    // Condizione di controllo "audio": stesso labirinto, stesse piastrelle, STESSA sosta,
    // ma l'esito arriva come suono invece che come temperatura.
    //
    // Il timing identico non e' un dettaglio: e' cio' che rende il confronto attribuibile
    // al canale sensoriale e non alla durata dell'interazione. Per questo la sosta sta
    // nella base comune e qui si specializza solo il momento della risposta.
    public class AudioJunctionCue : JunctionCueBase
    {
        private readonly AudioClip correctClip;
        private readonly AudioClip wrongClip;

        public override string CueName => "audio";

        public AudioJunctionCue(HapticProfile profile, AudioSource toneSource, AudioSource sfxSource,
                                AudioClip dwellTone, AudioClip readyTick, float sfxVolume,
                                AudioClip correctClip, AudioClip wrongClip)
            : base(profile, toneSource, sfxSource, dwellTone, readyTick, sfxVolume)
        {
            this.correctClip = correctClip;
            this.wrongClip = wrongClip;
        }

        protected override void OnReadingReady(bool correctBranch) =>
            PlaySfx(correctBranch ? correctClip : wrongClip);
    }
}
