using System;
using Demo6.Core.Random;

namespace Demo6.Core.Combat
{
    /// <summary>기획 5-2 피해 공식. 수치는 10배 정수 단위.</summary>
    public static class DamageMath
    {
        public const int DefenseK = 1000;
        public const double RollMin = 0.92;
        public const double RollMax = 1.08;
        public const double BaseCritChance = 0.05;
        public const double BaseCritDamage = 1.5;

        public static double Roll(IRandom rng) => RollMin + (RollMax - RollMin) * rng.NextDouble();

        public static double DefenseFactor(int defense) => DefenseK / (double)(DefenseK + Math.Max(0, defense));

        /// <summary>0.5는 올림.</summary>
        public static int RoundHalfUp(double value) => (int)Math.Floor(value + 0.5);

        /// <param name="hitPercent">기술 배율(%). 기본공격 100, 회오리 1타 90, 검풍 350.</param>
        /// <param name="critDamage">치명이면 곱할 배율(1.5 = 150%). 치명이 아니면 무시.</param>
        public static int ToMonster(double attack, double hitPercent, bool isCrit, double critDamage, double roll,
            int targetDefense = 0, double skillDamageBonus = 0, bool isSkill = false, double bossDamageBonus = 0, bool isBoss = false)
        {
            double value = attack * hitPercent / 100.0
                * (isSkill ? 1 + skillDamageBonus : 1)
                * (isBoss ? 1 + bossDamageBonus : 1)
                * (isCrit ? critDamage : 1)
                * roll
                * DefenseFactor(targetDefense);
            return Math.Max(1, RoundHalfUp(value));
        }

        /// <param name="patternPercent">패턴 배율(%). 보스 내려찍기 250 등. 일반 공격 100.</param>
        public static int ToPlayer(double monsterAttack, double patternPercent, double roll, int playerDefense)
        {
            double value = monsterAttack * patternPercent / 100.0 * roll * DefenseFactor(playerDefense);
            return Math.Max(1, RoundHalfUp(value));
        }
    }
}
