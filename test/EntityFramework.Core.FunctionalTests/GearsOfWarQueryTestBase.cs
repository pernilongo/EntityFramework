// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Data.Entity.FunctionalTests.TestModels.GearsOfWarModel;
using Microsoft.Data.Entity.FunctionalTests.TestUtilities.Xunit;
using Xunit;
// ReSharper disable ReplaceWithSingleCallToSingle

namespace Microsoft.Data.Entity.FunctionalTests
{
    public abstract class GearsOfWarQueryTestBase<TTestStore, TFixture> : IClassFixture<TFixture>, IDisposable
        where TTestStore : TestStore
        where TFixture : GearsOfWarQueryFixtureBase<TTestStore>, new()
    {
        [Fact]
        public virtual void Include_multiple_one_to_one_and_one_to_many()
        {
            using (var context = CreateContext())
            {
                var query = context.Tags.Include(t => t.Gear.Weapons);
                var result = query.ToList();

                Assert.Equal(6, result.Count);

                var gears = result.Select(t => t.Gear).Where(g => g != null).ToList();
                Assert.Equal(5, gears.Count);

                Assert.True(gears.All(g => g.Weapons.Any()));
            }
        }

        [Fact]
        public virtual void Include_multiple_one_to_one_and_one_to_many_self_reference()
        {
            using (var context = CreateContext())
            {
                var query = context.Tags.Include(t => t.Gear.Weapons);
                var result = query.ToList();

                Assert.Equal(6, result.Count);

                var gears = result.Select(t => t.Gear).Where(g => g != null).ToList();
                Assert.Equal(5, gears.Count);

                Assert.True(gears.All(g => g.Weapons != null));
            }
        }

        [Fact]
        public virtual void Include_multiple_one_to_one_optional_and_one_to_one_required()
        {
            using (var context = CreateContext())
            {
                var query = context.Tags.Include(t => t.Gear.Squad);
                var result = query.ToList();

                Assert.Equal(6, result.Count);
            }
        }

        [Fact]
        public virtual void Include_multiple_one_to_one_and_one_to_one_and_one_to_many()
        {
            using (var context = CreateContext())
            {
                var query = context.Tags.Include(t => t.Gear.Squad.Members);
                var result = query.ToList();

                Assert.Equal(6, result.Count);
            }
        }

        [Fact]
        public virtual void Include_multiple_circular()
        {
            using (var context = CreateContext())
            {
                var query = context.Gears.Include(g => g.CityOfBirth.StationedGears);
                var result = query.ToList();

                Assert.Equal(5, result.Count);

                var cities = result.Select(g => g.CityOfBirth).ToList();
                Assert.True(cities.All(c => c != null));
                Assert.True(cities.All(c => c.BornGears != null));
            }
        }

        [Fact]
        public virtual void Include_multiple_circular_with_filter()
        {
            using (var context = CreateContext())
            {
                var query = context.Gears.Include(g => g.CityOfBirth.StationedGears).Where(g => g.Nickname == "Marcus");
                var result = query.ToList();

                Assert.Equal(1, result.Count);
                Assert.Equal("Jacinto", result.Single().CityOfBirth.Name);
                Assert.Equal(2, result.Single().CityOfBirth.StationedGears.Count);
            }
        }

        [Fact]
        public virtual void Include_using_alternate_key()
        {
            using (var context = CreateContext())
            {
                var query = context.Gears.Include(g => g.Weapons).Where(g => g.Nickname == "Marcus");
                var result = query.ToList();

                Assert.Equal(1, result.Count);

                var weapons = result.Single().Weapons.ToList();
                Assert.Equal(2, weapons.Count);
                Assert.Contains("Marcus' Lancer", weapons.Select(w => w.Name));
                Assert.Contains("Marcus' Gnasher", weapons.Select(w => w.Name));
            }
        }

        [Fact]
        public virtual void Include_multiple_include_then_include()
        {
            var gearAssignedCities = new Dictionary<string, string>();
            var gearCitiesOfBirth = new Dictionary<string, string>();
            var gearTagNotes = new Dictionary<string, string>();
            var cityStationedGears = new Dictionary<string, List<string>>();
            var cityBornGears = new Dictionary<string, List<string>>();

            using (var context = CreateContext())
            {
                gearAssignedCities = context.Gears
                    .Include(g => g.AssignedCity)
                    .ToDictionary(g => g.Nickname, g => g.AssignedCity?.Name);

                gearCitiesOfBirth = context.Gears
                    .Include(g => g.CityOfBirth)
                    .ToDictionary(g => g.Nickname, g => g.CityOfBirth?.Name);

                gearTagNotes = context.Gears
                    .Include(g => g.Tag)
                    .ToDictionary(g => g.Nickname, g => g.Tag.Note);

                cityBornGears = context.Cities
                    .Include(c => c.BornGears)
                    .ToDictionary(
                        c => c.Name,
                        c => c.BornGears != null
                            ? c.BornGears.Select(g => g.Nickname).ToList()
                            : new List<string>());

                cityStationedGears = context.Cities
                    .Include(c => c.StationedGears)
                    .ToDictionary(
                        c => c.Name,
                        c => c.StationedGears != null
                            ? c.StationedGears.Select(g => g.Nickname).ToList()
                            : new List<string>());
            }

            ClearLog();

            using (var context = CreateContext())
            {
                var query = context.Gears
                    .Include(g => g.AssignedCity.BornGears).ThenInclude(g => g.Tag)
                    .Include(g => g.AssignedCity.StationedGears).ThenInclude(g => g.Tag)
                    .Include(g => g.CityOfBirth.BornGears).ThenInclude(g => g.Tag)
                    .Include(g => g.CityOfBirth.StationedGears).ThenInclude(g => g.Tag)
                    .OrderBy(g => g.Nickname);

                var result = query.ToList();

                var expectedGearCount = 5;
                Assert.Equal(expectedGearCount, result.Count);
                Assert.Equal("Baird", result[0].Nickname);
                Assert.Equal("Cole Train", result[1].Nickname);
                Assert.Equal("Dom", result[2].Nickname);
                Assert.Equal("Marcus", result[3].Nickname);
                Assert.Equal("Paduk", result[4].Nickname);

                for (var i = 0; i < expectedGearCount; i++)
                {
                    Assert.Equal(gearAssignedCities[result[i]?.Nickname], result[i].AssignedCity?.Name);
                    Assert.Equal(gearCitiesOfBirth[result[i]?.Nickname], result[i].CityOfBirth?.Name);

                    var assignedCity = result[i].AssignedCity;
                    if (assignedCity != null)
                    {
                        Assert.Equal(cityBornGears[assignedCity.Name].Count, assignedCity.BornGears.Count);
                        foreach (var bornGear in assignedCity.BornGears)
                        {
                            Assert.True(cityBornGears[assignedCity.Name].Contains(bornGear.Nickname));
                            Assert.Equal(gearTagNotes[bornGear.Nickname], bornGear.Tag.Note);
                        }

                        Assert.Equal(cityStationedGears[assignedCity.Name].Count, assignedCity.StationedGears.Count);
                        foreach (var stationedGear in assignedCity.StationedGears)
                        {
                            Assert.True(cityStationedGears[assignedCity.Name].Contains(stationedGear.Nickname));
                            Assert.Equal(gearTagNotes[stationedGear.Nickname], stationedGear.Tag.Note);
                        }
                    }

                    var cityOfBirth = result[i].CityOfBirth;
                    if (cityOfBirth != null)
                    {
                        Assert.Equal(cityBornGears[cityOfBirth.Name].Count, cityOfBirth.BornGears.Count);
                        foreach (var bornGear in cityOfBirth.BornGears)
                        {
                            Assert.True(cityBornGears[cityOfBirth.Name].Contains(bornGear.Nickname));
                            Assert.Equal(gearTagNotes[bornGear.Nickname], bornGear.Tag.Note);
                        }

                        Assert.Equal(cityStationedGears[cityOfBirth.Name].Count, cityOfBirth.StationedGears.Count);
                        foreach (var stationedGear in cityOfBirth.StationedGears)
                        {
                            Assert.True(cityStationedGears[cityOfBirth.Name].Contains(stationedGear.Nickname));
                            Assert.Equal(gearTagNotes[stationedGear.Nickname], stationedGear.Tag.Note);
                        }
                    }
                }
            }
        }

        [Fact]
        public virtual void Include_navigation_on_derived_type()
        {
            using (var context = CreateContext())
            {
                var query = context.Gears.OfType<Officer>().Include(o => o.Reports);
                var result = query.ToList();

                Assert.Equal(2, result.Count);

                var marcusReports = result.Where(e => e.Nickname == "Marcus").Single().Reports.ToList();
                Assert.Equal(3, marcusReports.Count);
                Assert.Contains("Baird", marcusReports.Select(g => g.Nickname));
                Assert.Contains("Cole Train", marcusReports.Select(g => g.Nickname));
                Assert.Contains("Dom", marcusReports.Select(g => g.Nickname));

                var bairdReports = result.Where(e => e.Nickname == "Baird").Single().Reports.ToList();
                Assert.Equal(1, bairdReports.Count);
                Assert.Contains("Paduk", bairdReports.Select(g => g.Nickname));
            }
        }

        [Fact]
        public virtual void Select_Where_Navigation_Included()
        {
            using (var context = CreateContext())
            {
                var cogTags
                    = (from ct in context.Set<CogTag>().Include(o => o.Gear)
                       where ct.Gear.Nickname == "Marcus"
                       select ct).ToList();

                Assert.Equal(1, cogTags.Count);
                Assert.True(cogTags.All(o => o.Gear != null));
            }
        }

        [Fact]
        public virtual void Include_with_join_reference1()
        {
            using (var context = CreateContext())
            {
                var query = context.Gears.Join(
                    context.Tags,
                    g => new { SquadId = (int?)g.SquadId, g.Nickname },
                    t => new { SquadId = t.GearSquadId, Nickname = t.GearNickName },
                    (g, t) => g).Include(g => g.CityOfBirth);

                var result = query.ToList();
                Assert.Equal(5, result.Count);
                Assert.True(result.All(g => g.CityOfBirth != null));
            }
        }

        [ConditionalFact]
        [MonoVersionCondition(Min = "4.2.0", SkipReason = "Queries fail on Mono < 4.2.0 due to differences in the implementation of LINQ")]
        public virtual void Include_with_join_reference2()
        {
            using (var context = CreateContext())
            {
                var query = context.Tags.Join(
                    context.Gears,
                    t => new { SquadId = t.GearSquadId, Nickname = t.GearNickName },
                    g => new { SquadId = (int?)g.SquadId, g.Nickname },
                    (t, g) => g).Include(g => g.CityOfBirth);

                var result = query.ToList();
                Assert.Equal(5, result.Count);
                Assert.True(result.All(g => g.CityOfBirth != null));
            }
        }

        [Fact]
        public virtual void Include_with_join_collection1()
        {
            using (var context = CreateContext())
            {
                var query = context.Gears.Join(
                    context.Tags,
                    g => new { SquadId = (int?)g.SquadId, g.Nickname },
                    t => new { SquadId = t.GearSquadId, Nickname = t.GearNickName },
                    (g, t) => g).Include(g => g.Weapons);

                var result = query.ToList();
                Assert.Equal(5, result.Count);
                Assert.True(result.All(g => g.Weapons.Count > 0));
            }
        }

        [ConditionalFact]
        [MonoVersionCondition(Min = "4.2.0", SkipReason = "Queries fail on Mono < 4.2.0 due to differences in the implementation of LINQ")]
        public virtual void Include_with_join_collection2()
        {
            using (var context = CreateContext())
            {
                var query = context.Tags.Join(
                    context.Gears,
                    t => new { SquadId = t.GearSquadId, Nickname = t.GearNickName },
                    g => new { SquadId = (int?)g.SquadId, g.Nickname },
                    (t, g) => g).Include(g => g.Weapons);

                var result = query.ToList();
                Assert.Equal(5, result.Count);
                Assert.True(result.All(g => g.Weapons.Count > 0));
            }
        }

        [Fact]
        public virtual void Include_with_join_multi_level()
        {
            using (var context = CreateContext())
            {
                var query = context.Gears.Join(
                    context.Tags,
                    g => new { SquadId = (int?)g.SquadId, g.Nickname },
                    t => new { SquadId = t.GearSquadId, Nickname = t.GearNickName },
                    (g, t) => g).Include(g => g.CityOfBirth.StationedGears);

                var result = query.ToList();
                Assert.Equal(5, result.Count);
                Assert.True(result.All(g => g.CityOfBirth != null));
            }
        }

        [Fact]
        public virtual void Include_with_join_and_inheritance1()
        {
            using (var context = CreateContext())
            {
                var query = context.Tags.Join(
                    context.Gears.OfType<Officer>(),
                    t => new { SquadId = t.GearSquadId, Nickname = t.GearNickName },
                    o => new { SquadId = (int?)o.SquadId, o.Nickname },
                    (t, o) => o).Include(o => o.CityOfBirth);

                var result = query.ToList();
                Assert.Equal(2, result.Count);
                Assert.True(result.All(o => o.CityOfBirth != null));
            }
        }

        [Fact]
        public virtual void Include_with_join_and_inheritance2()
        {
            using (var context = CreateContext())
            {
                var query = context.Gears.OfType<Officer>().Join(
                    context.Tags,
                    o => new { SquadId = (int?)o.SquadId, o.Nickname },
                    t => new { SquadId = t.GearSquadId, Nickname = t.GearNickName },
                    (o, t) => o).Include(g => g.Weapons);

                var result = query.ToList();
                Assert.Equal(2, result.Count);
                Assert.True(result.All(o => o.Weapons.Count > 0));
            }
        }

        [Fact]
        public virtual void Include_with_join_and_inheritance3()
        {
            using (var context = CreateContext())
            {
                var query = context.Tags.Join(
                    context.Gears.OfType<Officer>(),
                    t => new { SquadId = t.GearSquadId, Nickname = t.GearNickName },
                    g => new { SquadId = (int?)g.SquadId, g.Nickname },
                    (t, o) => o).Include(o => o.Reports);

                var result = query.ToList();
                Assert.Equal(2, result.Count);
                Assert.True(result.All(o => o.Reports.Count > 0));
            }
        }

        [Fact]
        public virtual void Include_with_nested_navigation_in_order_by()
        {
            using (var context = CreateContext())
            {
                var query = context.Weapons
                    .Include(w => w.Owner)
                    .OrderBy(e => e.Owner.CityOfBirth.Name);

                var result = query.ToList();
                Assert.Equal(9, result.Count);
                Assert.Equal("Ephyra", result[0].Owner.CityOrBirthName);
                Assert.Equal("Ephyra", result[1].Owner.CityOrBirthName);
                Assert.Equal("Hanover", result[2].Owner.CityOrBirthName);
                Assert.Equal("Hanover", result[3].Owner.CityOrBirthName);
                Assert.Equal("Jacinto", result[4].Owner.CityOrBirthName);
                Assert.Equal("Jacinto", result[5].Owner.CityOrBirthName);
                Assert.Equal("Unknown", result[6].Owner.CityOrBirthName);
                Assert.Equal("Unknown", result[7].Owner.CityOrBirthName);
                Assert.Equal("Unknown", result[8].Owner.CityOrBirthName);
            }
        }

        [Fact]
        public virtual void Where_enum()
        {
            using (var context = CreateContext())
            {
                var gears = context.Gears
                    .Where(g => g.Rank == MilitaryRank.Sergeant)
                    .ToList();

                Assert.Equal(1, gears.Count);
            }
        }

        [Fact]
        public virtual void Where_nullable_enum_with_constant()
        {
            using (var context = CreateContext())
            {
                var weapons = context.Weapons
                    .Where(w => w.AmmunitionType == AmmunitionType.Cartridge)
                    .ToList();

                Assert.Equal(5, weapons.Count);
            }
        }

        [Fact]
        public virtual void Where_nullable_enum_with_null_constant()
        {
            using (var context = CreateContext())
            {
                var weapons = context.Weapons
                    .Where(w => w.AmmunitionType == null)
                    .ToList();

                Assert.Equal(1, weapons.Count);
            }
        }

        //[Fact] Issue #3424
        public virtual void Where_nullable_enum_with_non_nullable_parameter()
        {
            var ammunitionType = AmmunitionType.Cartridge;

            using (var context = CreateContext())
            {
                var weapons = context.Weapons
                    .Where(w => w.AmmunitionType == ammunitionType)
                    .ToList();

                Assert.Equal(5, weapons.Count);
            }
        }

        [Fact]
        public virtual void Where_nullable_enum_with_nullable_parameter()
        {
            AmmunitionType? ammunitionType = AmmunitionType.Cartridge;

            using (var context = CreateContext())
            {
                var weapons = context.Weapons
                    .Where(w => w.AmmunitionType == ammunitionType)
                    .ToList();

                Assert.Equal(5, weapons.Count);
            }

            ammunitionType = null;

            using (var context = CreateContext())
            {
                var weapons = context.Weapons
                    .Where(w => w.AmmunitionType == ammunitionType)
                    .ToList();

                Assert.Equal(1, weapons.Count);
            }
        }

        [Fact]
        public virtual void Select_Where_Navigation()
        {
            using (var context = CreateContext())
            {
                var cogTags
                    = (from ct in context.Set<CogTag>()
                        where ct.Gear.Nickname == "Marcus"
                        select ct).ToList();

                Assert.Equal(1, cogTags.Count);
            }
        }

        [Fact]
        public virtual void Select_Where_Navigation_Scalar_Equals_Navigation_Scalar()
        {
            using (var context = CreateContext())
            {
                var cogTags
                    = (from ct1 in context.Set<CogTag>()
                        from ct2 in context.Set<CogTag>()
                        where ct1.Gear.Nickname == ct2.Gear.Nickname
                        select new { ct1, ct2 }).ToList();

                Assert.Equal(5, cogTags.Count);
            }
        }

        [Fact]
        public virtual void Select_Where_Navigation_Scalar_Equals_Navigation_Scalar_Projected()
        {
            using (var context = CreateContext())
            {
                var cogTags
                    = (from ct1 in context.Set<CogTag>()
                        from ct2 in context.Set<CogTag>()
                        where ct1.Gear.Nickname == ct2.Gear.Nickname
                        select new { ct1.Id, C2 = ct2.Id }).ToList();

                Assert.Equal(5, cogTags.Count);
            }
        }

        [Fact]
        public virtual void Select_Where_Navigation_Client()
        {
            using (var context = CreateContext())
            {
                var cogTags
                    = (from o in context.Set<CogTag>()
                        where o.Gear.IsMarcus
                        select o).ToList();

                Assert.Equal(1, cogTags.Count);
            }
        }

        [Fact]
        public virtual void Select_Where_Navigation_Null()
        {
            using (var context = CreateContext())
            {
                var cogTags
                    = (from ct in context.Set<CogTag>()
                        where ct.Gear == null
                        select ct).ToList();

                Assert.Equal(1, cogTags.Count);
            }
        }

        [Fact]
        public virtual void Select_Where_Navigation_Null_Reverse()
        {
            using (var context = CreateContext())
            {
                var cogTags
                    = (from ct in context.Set<CogTag>()
                        where null == ct.Gear
                        select ct).ToList();

                Assert.Equal(1, cogTags.Count);
            }
        }

        [Fact]
        public virtual void Select_Where_Navigation_Equals_Navigation()
        {
            using (var context = CreateContext())
            {
                var cogTags
                    = (from ct1 in context.Set<CogTag>()
                        from ct2 in context.Set<CogTag>()
                        where ct1.Gear == ct2.Gear
                        select new { ct1, ct2 }).ToList();

                Assert.Equal(6, cogTags.Count);
            }
        }

        [Fact]
        public virtual void Singleton_Navigation_With_Member_Access()
        {
            using (var context = CreateContext())
            {
                var cogTags
                    = (from ct in context.Set<CogTag>()
                        where ct.Gear.Nickname == "Marcus"
                        where ct.Gear.CityOrBirthName != "Ephyra"
                        select new { B = ct.Gear.CityOrBirthName }).ToList();

                Assert.Equal(1, cogTags.Count);
                Assert.True(cogTags.All(o => o.B != null));
            }
        }

        [Fact]
        public virtual void Select_Singleton_Navigation_With_Member_Access()
        {
            using (var context = CreateContext())
            {
                var cogTags
                    = (from ct in context.Set<CogTag>()
                        where ct.Gear.Nickname == "Marcus"
                        where ct.Gear.CityOrBirthName != "Ephyra"
                        select new { A = ct.Gear, B = ct.Gear.CityOrBirthName }).ToList();

                Assert.Equal(1, cogTags.Count);
                Assert.True(cogTags.All(o => o.A != null && o.B != null));
            }
        }

        [Fact]
        public virtual void GroupJoin_Composite_Key()
        {
            using (var context = CreateContext())
            {
                var gears
                    = (from ct in context.Set<CogTag>()
                        join g in context.Set<Gear>()
                            on new { N = ct.GearNickName, S = ct.GearSquadId }
                            equals new { N = g.Nickname, S = (int?)g.SquadId } into gs
                        from g in gs
                        select g).ToList();

                Assert.Equal(5, gears.Count);
            }
        }

        [ConditionalFact]
        [MonoVersionCondition(Min = "4.2.0", SkipReason = "Queries fail on Mono < 4.2.0 due to differences in the implementation of LINQ")]
        public virtual void Join_navigation_translated_to_subquery_composite_key()
        {
            List<Gear> gears;
            List<CogTag> tags;
            using (var context = CreateContext())
            {
                gears = context.Gears.ToList();
                tags = context.Tags.Include(e => e.Gear).ToList();
            }

            ClearLog();

            using (var context = CreateContext())
            {
                var query = from g in context.Gears
                            join t in context.Tags on g.FullName equals t.Gear.FullName
                            select new { g.FullName, t.Note };

                var result = query.ToList();

                var expected = (from g in gears
                                join t in tags on g.FullName equals t.Gear?.FullName
                                select new { g.FullName, t.Note }).ToList();

                Assert.Equal(expected.Count, result.Count);
                foreach (var resultItem in result)
                {
                    Assert.True(expected.Contains(resultItem));
                }
            }
        }

        [Fact]
        public virtual void Collection_with_inheritance_and_join_include_joined()
        {
            using (var context = CreateContext())
            {
                var query = (from t in context.Tags
                             join g in context.Gears.OfType<Officer>() on new { id1 = t.GearSquadId, id2 = t.GearNickName } 
                                equals new { id1 = (int?)g.SquadId, id2 = g.Nickname }
                             select g).Include(g => g.Tag);

                var result = query.ToList();

                Assert.NotNull(result);
            }
        }

        [Fact]
        public virtual void Collection_with_inheritance_and_join_include_source()
        {
            using (var context = CreateContext())
            {
                var query = (from g in context.Gears.OfType<Officer>()
                             join t in context.Tags on new { id1 = (int?)g.SquadId, id2 = g.Nickname } 
                                equals new { id1 = t.GearSquadId, id2 = t.GearNickName }
                             select g).Include(g => g.Tag);

                var result = query.ToList();

                Assert.NotNull(result);
            }
        }

        protected GearsOfWarContext CreateContext() => Fixture.CreateContext(TestStore);

        protected GearsOfWarQueryTestBase(TFixture fixture)
        {
            Fixture = fixture;

            TestStore = Fixture.CreateTestStore();
        }

        protected TFixture Fixture { get; }

        protected TTestStore TestStore { get; }

        protected virtual void ClearLog()
        {
        }

        public void Dispose() => TestStore.Dispose();
    }
}
