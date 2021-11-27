using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using GuitarProToMidi.Native;
using Xunit;

namespace GuitarProToMidi_UnitTests;

public class BendingPlanUnitTests
{
    [Theory]
    [ClassData(typeof(CreateTestCollection))]
    public void Create_validInput_correctBendingPlan(
        CreateCompositeTestData compositeTestData)
    {
        var actualBendingPlan = BendingPlan.create(
            compositeTestData.InputBendPoints.ToList(),
            compositeTestData.OriginalChannel,
            compositeTestData.UsedChannel,
            compositeTestData.Duration,
            compositeTestData.Index,
            compositeTestData.Resize,
            compositeTestData.IsVibrato);

        Assert.Equal(
            new BendingPlan(compositeTestData.OriginalChannel, compositeTestData.UsedChannel,
                compositeTestData.ExpectedBendPoints.ToList()), actualBendingPlan);
    }

    public record CreateCompositeTestData(
        ImmutableList<GuitarProToMidi.Native.BendPoint> InputBendPoints,
        ImmutableList<GuitarProToMidi.Native.BendPoint> ExpectedBendPoints,
        int OriginalChannel,
        int UsedChannel,
        int Duration,
        int Index,
        float Resize,
        bool IsVibrato);

    public class CreateTestCollection : TheoryData<CreateCompositeTestData>
    {
        private readonly CreateCompositeTestData _defaultTestData = new(
            ImmutableList<GuitarProToMidi.Native.BendPoint>.Empty.Add(new(0, 0.0f, 0)),
            ImmutableList<GuitarProToMidi.Native.BendPoint>.Empty.Add(new(0, 0.0f, 0)),
            0,
            0,
            0,
            0,
            1.0f,
            false);

        public CreateTestCollection()
        {
            Add(_defaultTestData);
            Add(_defaultTestData with
            {
                InputBendPoints = _defaultTestData.InputBendPoints.Add(new(5, 0.5f, _defaultTestData.UsedChannel)),
                ExpectedBendPoints = _defaultTestData.ExpectedBendPoints.AddRange(
                    new List<GuitarProToMidi.Native.BendPoint>
                    {
                            new(1, 0.1f, _defaultTestData.UsedChannel),
                            new(2, 0.2f, _defaultTestData.UsedChannel),
                            new(3, 0.3f, _defaultTestData.UsedChannel),
                            new(4, 0.4f, _defaultTestData.UsedChannel),
                            new(5, 0.5f, _defaultTestData.UsedChannel),
                            new(10, 0.5f, _defaultTestData.UsedChannel)
                    }),
                Duration = 10
            });
            Add(_defaultTestData with
            {
                InputBendPoints = _defaultTestData.InputBendPoints.Add(new(10, 1.0f, _defaultTestData.UsedChannel)),
                ExpectedBendPoints = _defaultTestData.ExpectedBendPoints.AddRange(
                    new List<GuitarProToMidi.Native.BendPoint>
                    {
                            new(1, 0.1f, _defaultTestData.UsedChannel),
                            new(2, 0.2f, _defaultTestData.UsedChannel),
                            new(3, 0.3f, _defaultTestData.UsedChannel),
                            new(4, 0.4f, _defaultTestData.UsedChannel),
                            new(5, 0.5f, _defaultTestData.UsedChannel),
                            new(6, 0.6f, _defaultTestData.UsedChannel),
                            new(7, 0.7f, _defaultTestData.UsedChannel),
                            new(8, 0.8f, _defaultTestData.UsedChannel),
                            new(9, 0.9f, _defaultTestData.UsedChannel),
                            new(10, 1.0f, _defaultTestData.UsedChannel)
                    }
                ),
                Duration = 10,
            });
            Add(_defaultTestData with
            {
                InputBendPoints = ImmutableList<GuitarProToMidi.Native.BendPoint>.Empty,
                ExpectedBendPoints = _defaultTestData.ExpectedBendPoints.AddRange(
                    new List<GuitarProToMidi.Native.BendPoint>
                    {
                            new(1, 6.0f, _defaultTestData.UsedChannel),
                            new(2, 12.0f, _defaultTestData.UsedChannel),
                            new(3, 6.0f, _defaultTestData.UsedChannel),
                            new(4, 0.0f, _defaultTestData.UsedChannel),
                            new(5, -6.0f, _defaultTestData.UsedChannel),
                            new(6, -12.0f, _defaultTestData.UsedChannel),
                            new(7, -6.0f, _defaultTestData.UsedChannel),
                            new(8, 0.0f, _defaultTestData.UsedChannel),
                            new(9, 6.0f, _defaultTestData.UsedChannel),
                            new(10, 12.0f, _defaultTestData.UsedChannel)
                    }
                ),
                Duration = 10,
                IsVibrato = true
            });
            Add(_defaultTestData with
            {
                InputBendPoints =
                ImmutableList<GuitarProToMidi.Native.BendPoint>.Empty.AddRange(
                    new List<GuitarProToMidi.Native.BendPoint>
                    {
                            new(1, 0.0f, _defaultTestData.UsedChannel),
                            new(2, 0.2f, _defaultTestData.UsedChannel)
                    }),
                ExpectedBendPoints = ImmutableList<GuitarProToMidi.Native.BendPoint>.Empty.AddRange(
                    new List<GuitarProToMidi.Native.BendPoint>
                    {
                            new(1, 0.0f, _defaultTestData.UsedChannel),
                            new(2, 0.1f, _defaultTestData.UsedChannel),
                            new(3, 0.2f, _defaultTestData.UsedChannel),
                            new(11, 0.2f, _defaultTestData.UsedChannel)
                    }),
                Duration = 10,
                Index = 1,
                Resize = 2.0f
            });
        }
    }
}
