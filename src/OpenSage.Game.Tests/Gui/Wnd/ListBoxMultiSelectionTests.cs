using System;
using System.Collections.Generic;
using Xunit;
using OpenSage.Gui.Wnd.Controls;
using OpenSage.Mathematics;

namespace OpenSage.Tests.Gui.Wnd;

public class ListBoxMultiSelectionTests
{
    private ListBox CreateListBox(int itemCount = 5)
    {
        var listBox = new ListBox(new[] { 50, 50 });

        var items = new ListBoxDataItem[itemCount];
        for (int i = 0; i < itemCount; i++)
        {
            items[i] = new ListBoxDataItem(
                $"Item{i}",
                new[] { $"Column1_{i}", $"Column2_{i}" },
                ColorRgbaF.White
            );
        }

        listBox.Items = items;
        return listBox;
    }

    [Fact]
    public void ListBox_DefaultsSingleSelectMode()
    {
        var listBox = CreateListBox();
        Assert.False(listBox.MultiSelect);
    }

    [Fact]
    public void ListBox_CanEnableMultiSelectMode()
    {
        var listBox = CreateListBox();
        listBox.MultiSelect = true;
        Assert.True(listBox.MultiSelect);
    }

    [Fact]
    public void ListBox_SingleSelectMode_HasOneSelectedItem()
    {
        var listBox = CreateListBox();
        listBox.SelectedIndex = 2;

        var selections = listBox.SelectedIndices;
        Assert.Single(selections);
        Assert.Equal(2, selections[0]);
    }

    [Fact]
    public void ListBox_SingleSelectMode_ChangingSelectionClearsOldSelection()
    {
        var listBox = CreateListBox();
        listBox.SelectedIndex = 1;
        listBox.SelectedIndex = 3;

        var selections = listBox.SelectedIndices;
        Assert.Single(selections);
        Assert.Equal(3, selections[0]);
    }

    [Fact]
    public void ListBox_MultiSelectMode_CanSelectMultipleItems()
    {
        var listBox = CreateListBox();
        listBox.MultiSelect = true;

        // Set initial item (index 0 is set by default)
        listBox.SelectedIndex = 1;
        listBox.SelectedIndex = 3;

        var selections = listBox.SelectedIndices;
        Assert.Equal(3, selections.Length); // 0, 1, 3
        Assert.Contains(0, selections);
        Assert.Contains(1, selections);
        Assert.Contains(3, selections);
    }

    [Fact]
    public void ListBox_MultiSelectMode_ToggleRemovesSelection()
    {
        var listBox = CreateListBox();
        listBox.MultiSelect = true;

        // Select item 1
        listBox.SelectedIndex = 1;
        Assert.Contains(1, listBox.SelectedIndices);

        // Toggle item 1 (should remove it)
        var itemsArea = GetItemsArea(listBox);
        itemsArea.ToggleSelection(1);

        var selections = listBox.SelectedIndices;
        Assert.DoesNotContain(1, selections);
    }

    [Fact]
    public void ListBox_MultiSelectMode_CanAddAndRemoveSelections()
    {
        var listBox = CreateListBox(10);
        listBox.MultiSelect = true;
        var itemsArea = GetItemsArea(listBox);

        // Add multiple selections
        itemsArea.AddSelection(2);
        itemsArea.AddSelection(4);
        itemsArea.AddSelection(7);

        var selections = listBox.SelectedIndices;
        Assert.Equal(4, selections.Length); // 0, 2, 4, 7

        // Remove one
        itemsArea.RemoveSelection(4);
        selections = listBox.SelectedIndices;
        Assert.Equal(3, selections.Length);
        Assert.DoesNotContain(4, selections);
    }

    [Fact]
    public void ListBox_MultiSelectMode_CanClearAllSelections()
    {
        var listBox = CreateListBox();
        listBox.MultiSelect = true;
        var itemsArea = GetItemsArea(listBox);

        itemsArea.AddSelection(1);
        itemsArea.AddSelection(3);

        itemsArea.ClearSelections();

        var selections = listBox.SelectedIndices;
        Assert.Empty(selections);
    }

    [Fact]
    public void ListBox_MultiSelectMode_ToggleAddsIfNotSelected()
    {
        var listBox = CreateListBox();
        listBox.MultiSelect = true;
        var itemsArea = GetItemsArea(listBox);

        // Clear default selection and toggle item 2
        itemsArea.ClearSelections();
        itemsArea.ToggleSelection(2);

        var selections = listBox.SelectedIndices;
        Assert.Single(selections);
        Assert.Equal(2, selections[0]);
    }

    [Fact]
    public void ListBox_MultiSelectMode_IsItemSelectedWorks()
    {
        var listBox = CreateListBox();
        listBox.MultiSelect = true;
        var itemsArea = GetItemsArea(listBox);

        itemsArea.ClearSelections();
        itemsArea.AddSelection(2);

        Assert.False(itemsArea.IsItemSelected(0));
        Assert.False(itemsArea.IsItemSelected(1));
        Assert.True(itemsArea.IsItemSelected(2));
        Assert.False(itemsArea.IsItemSelected(3));
    }

    [Fact]
    public void ListBox_SwitchFromMultiToSingleMode_KeepsFirstSelection()
    {
        var listBox = CreateListBox();
        listBox.MultiSelect = true;
        var itemsArea = GetItemsArea(listBox);

        itemsArea.ClearSelections();
        itemsArea.AddSelection(1);
        itemsArea.AddSelection(3);
        itemsArea.AddSelection(4);

        Assert.Equal(3, listBox.SelectedIndices.Length);

        // Switch to single select
        listBox.MultiSelect = false;

        // Should keep first selection (1)
        var selections = listBox.SelectedIndices;
        Assert.Single(selections);
        Assert.Equal(1, selections[0]);
    }

    [Fact]
    public void ListBox_SwitchFromSingleToMultiMode_PreservesSelection()
    {
        var listBox = CreateListBox();
        listBox.SelectedIndex = 2;

        Assert.Single(listBox.SelectedIndices);

        // Switch to multi select
        listBox.MultiSelect = true;

        var selections = listBox.SelectedIndices;
        Assert.Single(selections);
        Assert.Equal(2, selections[0]);
    }

    [Fact]
    public void ListBox_MultiSelectMode_InvalidIndicesIgnored()
    {
        var listBox = CreateListBox();
        listBox.MultiSelect = true;
        var itemsArea = GetItemsArea(listBox);

        itemsArea.ClearSelections();

        // Try to add invalid indices
        itemsArea.AddSelection(-1);
        itemsArea.AddSelection(100);
        itemsArea.AddSelection(1); // Valid

        var selections = listBox.SelectedIndices;
        Assert.Single(selections);
        Assert.Equal(1, selections[0]);
    }

    [Fact]
    public void ListBox_MultiSelectMode_CannotAddDuplicateSelections()
    {
        var listBox = CreateListBox();
        listBox.MultiSelect = true;
        var itemsArea = GetItemsArea(listBox);

        itemsArea.ClearSelections();
        itemsArea.AddSelection(2);
        itemsArea.AddSelection(2); // Try to add again

        var selections = listBox.SelectedIndices;
        Assert.Single(selections);
        Assert.Equal(2, selections[0]);
    }

    [Fact]
    public void ListBox_MultiSelectMode_RemovalOfNonExistentSelectionIgnored()
    {
        var listBox = CreateListBox();
        listBox.MultiSelect = true;
        var itemsArea = GetItemsArea(listBox);

        itemsArea.ClearSelections();
        itemsArea.AddSelection(1);
        itemsArea.RemoveSelection(999); // Try to remove non-existent

        var selections = listBox.SelectedIndices;
        Assert.Single(selections);
        Assert.Equal(1, selections[0]);
    }

    [Fact]
    public void ListBox_MultiSelectMode_SelectedIndexReturnsLastSelected()
    {
        var listBox = CreateListBox();
        listBox.MultiSelect = true;
        var itemsArea = GetItemsArea(listBox);

        itemsArea.ClearSelections();
        itemsArea.AddSelection(1);
        itemsArea.AddSelection(3);
        itemsArea.AddSelection(2);

        // SelectedIndex should return the last one added
        Assert.Equal(2, itemsArea.SelectedIndex);
    }

    [Fact]
    public void ListBox_MultiSelectMode_SelectedIndicesInOrder()
    {
        var listBox = CreateListBox();
        listBox.MultiSelect = true;
        var itemsArea = GetItemsArea(listBox);

        itemsArea.ClearSelections();
        itemsArea.AddSelection(3);
        itemsArea.AddSelection(1);
        itemsArea.AddSelection(4);

        var selections = listBox.SelectedIndices;
        Assert.Equal(3, selections.Length);
        // Order should be: 3, 1, 4 (in order added)
        Assert.Equal(new[] { 3, 1, 4 }, selections);
    }

    [Fact]
    public void ListBox_EventFiresOnMultiSelectChange()
    {
        var listBox = CreateListBox();
        var itemsArea = GetItemsArea(listBox);

        var eventFired = false;
        listBox.SelectedIndexChanged += (s, e) =>
        {
            eventFired = true;
        };

        listBox.MultiSelect = true;
        itemsArea.AddSelection(2);

        Assert.True(eventFired);
    }

    [Fact]
    public void ListBox_EventFiresOnMultiSelectRemove()
    {
        var listBox = CreateListBox();
        listBox.MultiSelect = true;
        var itemsArea = GetItemsArea(listBox);

        itemsArea.ClearSelections();
        itemsArea.AddSelection(1);

        var eventFired = false;
        listBox.SelectedIndexChanged += (s, e) =>
        {
            eventFired = true;
        };

        itemsArea.RemoveSelection(1);

        Assert.True(eventFired);
    }

    [Fact]
    public void ListBox_MultiSelectMode_ClearSelectionsAfterLoad()
    {
        var listBox = CreateListBox();
        listBox.MultiSelect = true;

        // Creating new items should clear selections and set first item
        var newItems = new ListBoxDataItem[3];
        for (int i = 0; i < 3; i++)
        {
            newItems[i] = new ListBoxDataItem(
                $"NewItem{i}",
                new[] { $"Col1_{i}", $"Col2_{i}" },
                ColorRgbaF.White
            );
        }

        listBox.Items = newItems;

        var selections = listBox.SelectedIndices;
        Assert.Single(selections);
        Assert.Equal(0, selections[0]);
    }

    // Helper to access the private ListBoxItemsArea
    private ListBoxItemsArea GetItemsArea(ListBox listBox)
    {
        // Use reflection to get the private _itemsArea field
        var field = typeof(ListBox).GetField("_itemsArea",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        return (ListBoxItemsArea)field?.GetValue(listBox);
    }
}
