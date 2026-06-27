using System.Collections.Generic;
using System.Linq;
using DiskSpaceTracker.Services;
using DiskSpaceTracker.ViewModels;
using FluentAssertions;
using Xunit;

namespace DiskSpaceTracker.Tests.ViewModels;

public class ProcessPickerViewModelTests
{
    private sealed class StubProcesses : IProcessConditionService
    {
        private readonly IReadOnlyList<ProcessGroup> _list;
        public int ListCallCount;
        public StubProcesses(params ProcessGroup[] list) => _list = list;
        public bool Test(string? name, bool shouldExist) => false;
        public IReadOnlyList<ProcessGroup> ListProcesses(string? filterContains = null)
        {
            ListCallCount++;
            return _list;
        }
    }

    [Fact]
    public void Search_FiltersInMemory_WithoutReEnumerating()
    {
        var stub = new StubProcesses(
            new ProcessGroup("chrome", 3),
            new ProcessGroup("explorer", 1),
            new ProcessGroup("code", 2));

        var vm = new ProcessPickerViewModel(stub);
        stub.ListCallCount.Should().Be(1);           // one enumeration at construction
        vm.Items.Should().HaveCount(3);

        vm.Search = "ch";
        stub.ListCallCount.Should().Be(1);           // keystroke filters the cache, no re-enumeration
        vm.Items.Select(i => i.Name).Should().ContainSingle().Which.Should().Be("chrome");

        vm.Search = "";
        vm.Items.Should().HaveCount(3);              // clearing the filter restores the full list

        vm.Refresh();
        stub.ListCallCount.Should().Be(2);           // explicit Refresh re-enumerates
    }
}
