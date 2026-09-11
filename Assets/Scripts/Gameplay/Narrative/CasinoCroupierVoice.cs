using RogueDrive.Modifiers;
using UnityEngine;

namespace RogueDrive.Gameplay.Narrative
{
    /// <summary>
    /// Голос придорожного казино. Крупье комментирует визит, ставки и выпавшие
    /// модификаторы через штатную радиосвязь, а диспетчер «Маяк» ворчит про азартные
    /// игры в апокалипсис. Реплики выводятся только в заметные моменты, чтобы не
    /// забивать эфир: приезд, ставка выше обычной, редкий или эпический результат,
    /// синергия, гарантия и отъезд.
    /// </summary>
    public static class CasinoCroupierVoice
    {
        const string Beacon = "МАЯК // ЦИТАДЕЛЬ";

        static readonly string[] ArrivalLines =
        {
            "Добро пожаловать, водитель! Жетоны на стол — барабан сам решит, чем усилить вашу машину.",
            "Заходите, заходите! У нас единственное казино на трассе, где выигрыш прикручивают к кузову.",
            "Слышу мотор — значит, есть клиент. Ставьте жетоны, крутите барабан, и пусть трасса будет добра.",
            "О, живой! Редкость в наших краях. Жетоны есть? Тогда к барабану, не задерживайте очередь из зомби."
        };

        static readonly string[] NoTokensLines =
        {
            "Без жетонов, водитель? Барабан крутится только за опыт. Возвращайтесь, когда наберёте уровень.",
            "Пустые карманы — пустой барабан. Разберитесь с ордой до следующего пункта и приезжайте с жетонами."
        };

        static readonly string[] BeaconGrumbleLines =
        {
            "Скиталец, ты серьёзно? Казино? Посреди апокалипсиса? Ладно. Только быстро, орда не ждёт.",
            "Маяк на связи. Мы фиксируем твою остановку у игрового заведения. Комментировать не буду.",
            "Играешь на жетоны, пока Цитадель запечатывает шлюзы. Хорошо хоть выигрыш ставят на машину."
        };

        static readonly string[] RareBetLines =
        {
            "Два жетона — понимаю, обычного вам мало. Барабан заряжен только редкими.",
            "Ставка на редкость принята. Дешёвку из барабана убираем."
        };

        static readonly string[] SynergyBetLines =
        {
            "Три жетона на синергию! Смелый выбор. Барабан крутит только то, что замкнёт вашу связку.",
            "Ставка на синергию. Крупье одобряет: вы пришли не просто играть, а собирать машину."
        };

        static readonly string[] EpicLines =
        {
            "Эпический модуль! Такое выпадает раз в смену. Прикручиваем немедленно.",
            "Барабан выдал эпик. Поздравляю, водитель, орда сегодня пожалеет."
        };

        static readonly string[] RareLines =
        {
            "Редкий модуль. Хороший вечер для вас, водитель.",
            "Редкость! Не забудьте рассказать в Цитадели, где вам так везёт."
        };

        static readonly string[] SynergyLines =
        {
            "Связка замкнулась! Синергия собрана — вот за такие моменты я и держу это заведение.",
            "Синергия! Ваши модули заработали вместе. Крупье снимает шляпу."
        };

        static readonly string[] PityLines =
        {
            "Барабану надоело вас мучить — синергия по гарантии заведения. Мы честное казино.",
            "Сработала гарантия: столько спинов без связки не бывает даже у нас. Забирайте синергию."
        };

        static readonly string[] UpgradeLines =
        {
            "Тот же модуль, уровнем выше. Машина укомплектована — теперь только улучшения.",
            "Улучшение установленного модуля. Кузов полон, барабан работает на качество."
        };

        static readonly string[] LeaveLines =
        {
            "Жетоны потрачены. Счастливой дороги, водитель, следующий пункт ждёт через тысячу метров.",
            "Заведение благодарит за игру. Не забывайте: опыт с зомби — новые жетоны.",
            "Барабан остывает. Возвращайтесь живым — и с жетонами."
        };

        static int lastStopIndex = -1;
        static bool beaconGrumbled;

        /// <summary>Сброс на новую сцену: «Маяк» ворчит один раз за заезд.</summary>
        public static void ResetForRun()
        {
            lastStopIndex = -1;
            beaconGrumbled = false;
        }

        public static void OnArrival(BuffCasinoStop stop, int tokens)
        {
            if (stop == null) return;
            lastStopIndex = stop.StopIndex;

            Say(stop, tokens > 0 ? Pick(ArrivalLines) : Pick(NoTokensLines), 4.5f);

            if (!beaconGrumbled)
            {
                beaconGrumbled = true;
                RadioTransmissionSystem.Instance?.EnqueueTransmission(Beacon, Pick(BeaconGrumbleLines), 4.5f);
            }
        }

        public static void OnBet(string stopName, CasinoBet bet)
        {
            if (bet == CasinoBet.RareGuaranteed) Say(stopName, Pick(RareBetLines), 3.5f);
            else if (bet == CasinoBet.SynergyHunt) Say(stopName, Pick(SynergyBetLines), 3.5f);
        }

        public static void OnResult(string stopName, ModifierDefinition def, int newLevel,
                                    bool closesSynergy, bool pityFired, bool carFull)
        {
            if (def == null) return;

            if (pityFired) Say(stopName, Pick(PityLines), 4f);
            else if (closesSynergy) Say(stopName, Pick(SynergyLines), 4f);
            else if (def.Rarity == Rarity.Epic) Say(stopName, Pick(EpicLines), 3.5f);
            else if (def.Rarity == Rarity.Rare) Say(stopName, Pick(RareLines), 3f);
            else if (carFull && newLevel > 1) Say(stopName, Pick(UpgradeLines), 3.5f);
        }

        public static void OnLeave(string stopName)
        {
            Say(stopName, Pick(LeaveLines), 3.5f);
        }

        static void Say(BuffCasinoStop stop, string line, float duration) => Say(stop.StopName, line, duration);

        static void Say(string stopName, string line, float duration)
        {
            RadioTransmissionSystem radio = RadioTransmissionSystem.Instance;
            if (radio == null) return;

            string speaker = "КРУПЬЕ // " + (string.IsNullOrEmpty(stopName) ? "КАЗИНО" : stopName.ToUpperInvariant());
            radio.EnqueueTransmission(speaker, line, duration);
        }

        static string Pick(string[] lines)
        {
            if (lines == null || lines.Length == 0) return string.Empty;
            return lines[Random.Range(0, lines.Length)];
        }
    }
}
