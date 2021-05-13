using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using GuitarProToMidi;
using Xunit;

namespace GuitarProToMidi_UnitTests
{
    public class NativeFormatUnitTests
    {
        [Theory]
        [ClassData(typeof(CreateBendingPlanTestCollection))]
        public void CreateBendingPlan_bla_bla(CreateBendingPlanCompositeTestData compositeTestData)
        {
            var track = new GuitarProToMidi.Track();

            var actualBendingPlan = track.createBendingPlan(
                compositeTestData.InputBendPoints.ToList(),
                compositeTestData.OriginalChannel,
                compositeTestData.UsedChannel,
                compositeTestData.Duration,
                compositeTestData.Index,
                compositeTestData.Resize,
                compositeTestData.IsVibrato);

            // First assert is a subset of second one but provides better output for comparing bend points
            Assert.Equal(compositeTestData.ExpectedBendPoints, actualBendingPlan.bendingPoints);
            Assert.Equal(
                new BendingPlan(compositeTestData.OriginalChannel, compositeTestData.UsedChannel,
                    compositeTestData.ExpectedBendPoints.ToList()), actualBendingPlan);
        }

        public record CreateBendingPlanCompositeTestData(
            ImmutableList<GuitarProToMidi.BendPoint> InputBendPoints,
            ImmutableList<GuitarProToMidi.BendPoint> ExpectedBendPoints,
            int OriginalChannel,
            int UsedChannel,
            int Duration,
            int Index,
            float Resize,
            bool IsVibrato);

        public class CreateBendingPlanTestCollection : TheoryData<CreateBendingPlanCompositeTestData>
        {
            private CreateBendingPlanCompositeTestData _defaultTestData = new(
                ImmutableList<GuitarProToMidi.BendPoint>.Empty.Add(new()),
                ImmutableList<GuitarProToMidi.BendPoint>.Empty.Add(new()),
                0,
                0,
                0,
                0,
                1.0f,
                false);

            public CreateBendingPlanTestCollection()
            {
                Add(_defaultTestData);
                Add(_defaultTestData with
                {
                    InputBendPoints = _defaultTestData.InputBendPoints.Add(new(0.5f, 5)),
                    ExpectedBendPoints = _defaultTestData.ExpectedBendPoints.AddRange(
                        new List<GuitarProToMidi.BendPoint>
                        {
                            new(0.1f, 1),
                            new(0.2f, 2),
                            new(0.3f, 3),
                            new(0.4f, 4),
                            new(0.5f, 5),
                            new(0.5f, 10)
                        }),
                    Duration = 10
                });
                Add(_defaultTestData with
                {
                    InputBendPoints = _defaultTestData.InputBendPoints.Add(new(1.0f, 10)),
                    ExpectedBendPoints = _defaultTestData.ExpectedBendPoints.AddRange(
                        new List<GuitarProToMidi.BendPoint>
                        {
                            new(0.1f, 1),
                            new(0.2f, 2),
                            new(0.3f, 3),
                            new(0.4f, 4),
                            new(0.5f, 5),
                            new(0.6f, 6),
                            new(0.7f, 7),
                            new(0.8f, 8),
                            new(0.9f, 9),
                            new(1.0f, 10)
                        }
                    ),
                    Duration = 10,
                });
                Add(_defaultTestData with
                {
                    InputBendPoints = ImmutableList<GuitarProToMidi.BendPoint>.Empty,
                    ExpectedBendPoints = _defaultTestData.ExpectedBendPoints.AddRange(
                        new List<GuitarProToMidi.BendPoint>
                        {
                            new(6.0f, 1),
                            new(12.0f, 2),
                            new(6.0f, 3),
                            new(0.0f, 4),
                            new(-6.0f, 5),
                            new(-12.0f, 6),
                            new(-6.0f, 7),
                            new(0.0f, 8),
                            new(6.0f, 9),
                            new(12.0f, 10)
                        }
                    ),
                    Duration = 10,
                    IsVibrato = true
                });
                Add(_defaultTestData with
                {
                    InputBendPoints =
                    ImmutableList<GuitarProToMidi.BendPoint>.Empty.Add(new(0.0f, 1)).Add(new(0.2f, 2)),
                    ExpectedBendPoints = ImmutableList<GuitarProToMidi.BendPoint>.Empty.AddRange(
                        new List<GuitarProToMidi.BendPoint>
                        {
                            new(0.0f, 1),
                            new(0.1f, 2),
                            new(0.2f, 3),
                            new(0.2f, 11)
                        }),
                    Duration = 10,
                    Index = 1,
                    Resize = 2.0f
                });
            }
        }
    }
}
