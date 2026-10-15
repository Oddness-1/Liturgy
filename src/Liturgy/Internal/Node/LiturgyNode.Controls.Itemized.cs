using System;
using System.Reflection;
using Godot;
using static Liturgy.Rite;

namespace Liturgy.Internal;

internal sealed partial class LiturgyNode
{
    private void SetupItemSelectedCallbacks()
    {
        var methods = ScanForMethods<OnItemSelected>();
        
        foreach (var (method, attr) in methods)
        {
            var methodName = $"{method.DeclaringType?.FullName}.{method.Name}";
            var (callback, sourceType) = CreateItemSelectedCallback(method);
            if (callback is null) continue;
            
            var excludeSet = ParseExcludeSet(attr.Exclude);
            
            var typeName = sourceType?.Name ?? "ItemList/OptionButton";
            LiturgyReport.AddControlHook("OnItemSelected", methodName, attr.Target, attr.Exclude, attr.Priority, typeName);
            
            if (attr.Target is null)
            {
                _itemSelectedGlobalCallbacks.Add((callback, excludeSet, sourceType));
            }
            else
            {
                foreach (var t in attr.Target.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                {
                    var normalized = NormalizePath(t);
                    if (!_itemSelectedTargetedCallbacks.ContainsKey(normalized))
                        _itemSelectedTargetedCallbacks[normalized] = [];
                    _itemSelectedTargetedCallbacks[normalized].Add((callback, excludeSet));
                }
            }
        }
    }
    
    private static (Action<int, Control>? Callback, Type? SourceType) CreateItemSelectedCallback(MethodInfo method)
    {
        var parameters = method.GetParameters();
        var methodName = $"{method.DeclaringType?.FullName}.{method.Name}";
        
        int? intIdx = null;
        int? sourceIdx = null;
        Type? sourceType = null;
        var usesLong = false;
        
        for (var i = 0; i < parameters.Length; i++)
        {
            var paramType = parameters[i].ParameterType;
            
            if (paramType == typeof(int) || paramType == typeof(long))
            {
                intIdx = i;
                usesLong = paramType == typeof(long);
            }
            else if (typeof(ItemList).IsAssignableFrom(paramType) || 
                     typeof(OptionButton).IsAssignableFrom(paramType))
            {
                sourceIdx = i;
                sourceType = paramType;
            }
            else
            {
                LiturgyReport.AddInvalidParameterError(methodName, "OnItemSelected", parameters[i].Name ?? "parameters[i]", paramType, "int/long (index) and/or ItemList, OptionButton");
                return (null, null);
            }
        }
        
        // Handle long conversion if needed
        if (usesLong)
        {
            return CreateItemSelectedCallbackWithLong(method, intIdx, sourceIdx, sourceType);
        }
        
        var callback = DelegateFactory.CreateSignalCallback<int, Control>(method, sourceType, intIdx, sourceIdx);
        if (callback is not null) return (callback, sourceType);

        LiturgyReport.AddMethodError(methodName, "OnItemSelected", "Failed to create callback.");
        return (null, null);

    }
    
    private static (Action<int, Control>? Callback, Type? SourceType) CreateItemSelectedCallbackWithLong(
        MethodInfo method, int? intIdx, int? sourceIdx, Type? sourceType)
    {
        var parameters = method.GetParameters();
        
        if (parameters.Length == 1 && intIdx is not null)
        {
            var del = (Action<long>)Delegate.CreateDelegate(typeof(Action<long>), method);
            return ((index, _) => del(index), null);
        }
        
        if (intIdx is not null && sourceIdx is not null)
        {
            var nodeCallback = DelegateFactory.CreateCallback<Control>(method, sourceType);
            if (nodeCallback is null) return (null, sourceType);
            
            // We need to manually handle the long + source case
            if (intIdx < sourceIdx)
            {
                var delegateType = typeof(Action<,>).MakeGenericType(typeof(long), sourceType!);
                var typedDelegate = Delegate.CreateDelegate(delegateType, method);
                
                // Use MakeGenericMethod approach for the wrapper
                var wrapperMethod = typeof(LiturgyNode)
                    .GetMethod(nameof(BuildItemSelectedLongWrapper_SignalFirst), BindingFlags.NonPublic | BindingFlags.Static)!
                    .MakeGenericMethod(sourceType!);
                return ((Action<int, Control>?)wrapperMethod.Invoke(null, [typedDelegate]), sourceType);
            }
            else
            {
                var delegateType = typeof(Action<,>).MakeGenericType(sourceType!, typeof(long));
                var typedDelegate = Delegate.CreateDelegate(delegateType, method);
                
                var wrapperMethod = typeof(LiturgyNode)
                    .GetMethod(nameof(BuildItemSelectedLongWrapper_NodeFirst), BindingFlags.NonPublic | BindingFlags.Static)!
                    .MakeGenericMethod(sourceType!);
                return ((Action<int, Control>?)wrapperMethod.Invoke(null, [typedDelegate]), sourceType);
            }
        }
        
        return (null, sourceType);
    }
    
    private static Action<int, Control> BuildItemSelectedLongWrapper_SignalFirst<TSource>(Action<long, TSource> del)
        where TSource : Control
    {
        return (index, source) =>
        {
            if (source is TSource typed) del(index, typed);
        };
    }
    
    private static Action<int, Control> BuildItemSelectedLongWrapper_NodeFirst<TSource>(Action<TSource, long> del)
        where TSource : Control
    {
        return (index, source) =>
        {
            if (source is TSource typed) del(typed, index);
        };
    }
    private void SetupItemActivatedCallbacks()
    {
        var methods = ScanForMethods<OnItemActivated>();
        
        foreach (var (method, attr) in methods)
        {
            var methodName = $"{method.DeclaringType?.FullName}.{method.Name}";
            var callback = CreateItemActivatedCallback(method);
            if (callback is null) continue;
            
            var excludeSet = ParseExcludeSet(attr.Exclude);
            
            LiturgyReport.AddControlHook("OnItemActivated", methodName, attr.Target, attr.Exclude, attr.Priority, "ItemList");
            
            if (attr.Target is null)
            {
                _itemActivatedGlobalCallbacks.Add((callback, excludeSet));
            }
            else
            {
                foreach (var t in attr.Target.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                {
                    var normalized = NormalizePath(t);
                    if (!_itemActivatedTargetedCallbacks.ContainsKey(normalized))
                        _itemActivatedTargetedCallbacks[normalized] = [];
                    _itemActivatedTargetedCallbacks[normalized].Add((callback, excludeSet));
                }
            }
        }
    }
    
    private static Action<int, ItemList>? CreateItemActivatedCallback(MethodInfo method)
    {
        var parameters = method.GetParameters();
        var methodName = $"{method.DeclaringType?.FullName}.{method.Name}";
        
        int? intIdx = null;
        int? sourceIdx = null;
        var usesLong = false;
        
        for (var i = 0; i < parameters.Length; i++)
        {
            var paramType = parameters[i].ParameterType;

            if (paramType == typeof(int) || paramType == typeof(long))
            {
                intIdx = i;
                usesLong = paramType == typeof(long);
            }
            else if (typeof(ItemList).IsAssignableFrom(paramType)) sourceIdx = i;
            
            else
            {
                LiturgyReport.AddInvalidParameterError(methodName, "OnItemActivated", parameters[i].Name ?? "parameters[i]", paramType, "int/long (index) and/or ItemList");
                return null;
            }
        }
        
        if (parameters.Length == 0)
        {
            var del = (Action)Delegate.CreateDelegate(typeof(Action), method);
            return (_, _) => del();
        }
        
        if (intIdx is not null && sourceIdx is null)
        {
            if (usesLong)
            {
                var del = (Action<long>)Delegate.CreateDelegate(typeof(Action<long>), method);
                return (index, _) => del(index);
            }
            else
            {
                var del = (Action<int>)Delegate.CreateDelegate(typeof(Action<int>), method);
                return (index, _) => del(index);
            }
        }
        
        if (intIdx is null && sourceIdx is not null)
        {
            var del = (Action<ItemList>)Delegate.CreateDelegate(typeof(Action<ItemList>), method);
            return (_, source) => del(source);
        }
        
        if (intIdx is not null && sourceIdx is not null)
        {
            if (usesLong)
            {
                if (intIdx < sourceIdx)
                {
                    var del = (Action<long, ItemList>)Delegate.CreateDelegate(typeof(Action<long, ItemList>), method);
                    return (index, source) => del(index, source);
                }
                else
                {
                    var del = (Action<ItemList, long>)Delegate.CreateDelegate(typeof(Action<ItemList, long>), method);
                    return (index, source) => del(source, index);
                }
            }
            else
            {
                if (intIdx < sourceIdx)
                {
                    var del = (Action<int, ItemList>)Delegate.CreateDelegate(typeof(Action<int, ItemList>), method);
                    return del;
                }
                else
                {
                    var del = (Action<ItemList, int>)Delegate.CreateDelegate(typeof(Action<ItemList, int>), method);
                    return (index, source) => del(source, index);
                }
            }
        }
        
        LiturgyReport.AddMethodError(methodName, "OnItemActivated", "Failed to create callback.");
        return null;
    }
    private void SetupTabChangedCallbacks()
    {
        var methods = ScanForMethods<OnTabChanged>();
        
        foreach (var (method, attr) in methods)
        {
            var methodName = $"{method.DeclaringType?.FullName}.{method.Name}";
            var (callback, sourceType) = CreateTabChangedCallback(method);
            if (callback is null) continue;
            
            var excludeSet = ParseExcludeSet(attr.Exclude);
            
            var typeName = sourceType?.Name ?? "TabContainer/TabBar";
            LiturgyReport.AddControlHook("OnTabChanged", methodName, attr.Target, attr.Exclude, attr.Priority, typeName);
            
            if (attr.Target is null)
            {
                _tabChangedGlobalCallbacks.Add((callback, excludeSet, sourceType));
            }
            else
            {
                foreach (var t in attr.Target.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                {
                    var normalized = NormalizePath(t);
                    if (!_tabChangedTargetedCallbacks.ContainsKey(normalized))
                        _tabChangedTargetedCallbacks[normalized] = [];
                    _tabChangedTargetedCallbacks[normalized].Add((callback, excludeSet));
                }
            }
        }
    }
    
    private static (Action<int, Control>? Callback, Type? SourceType) CreateTabChangedCallback(MethodInfo method)
    {
        var parameters = method.GetParameters();
        var methodName = $"{method.DeclaringType?.FullName}.{method.Name}";
        
        int? intIdx = null;
        int? sourceIdx = null;
        Type? sourceType = null;
        var usesLong = false;
        
        for (var i = 0; i < parameters.Length; i++)
        {
            var paramType = parameters[i].ParameterType;
            
            if (paramType == typeof(int) || paramType == typeof(long))
            {
                intIdx = i;
                usesLong = paramType == typeof(long);
            }
            else if (typeof(TabContainer).IsAssignableFrom(paramType) || 
                     typeof(TabBar).IsAssignableFrom(paramType))
            {
                sourceIdx = i;
                sourceType = paramType;
            }
            else
            {
                LiturgyReport.AddInvalidParameterError(methodName, "OnTabChanged", parameters[i].Name ?? "parameters[i]", paramType, "int/long (index) and/or TabContainer, TabBar");
                return (null, null);
            }
        }
        
        // Handle long conversion if needed
        if (usesLong)
        {
            return CreateTabChangedCallbackWithLong(method, intIdx, sourceIdx, sourceType);
        }
        
        var callback = DelegateFactory.CreateSignalCallback<int, Control>(method, sourceType, intIdx, sourceIdx);
        if (callback is not null) return (callback, sourceType);

        LiturgyReport.AddMethodError(methodName, "OnTabChanged", "Failed to create callback.");
        return (null, null);

    }
    
    private static (Action<int, Control>? Callback, Type? SourceType) CreateTabChangedCallbackWithLong(
        MethodInfo method, int? intIdx, int? sourceIdx, Type? sourceType)
    {
        var parameters = method.GetParameters();
        
        if (parameters.Length == 1 && intIdx is not null)
        {
            var del = (Action<long>)Delegate.CreateDelegate(typeof(Action<long>), method);
            return ((index, _) => del(index), null);
        }

        if (intIdx is null || sourceIdx is null) return (null, sourceType);

        if (intIdx < sourceIdx)
        {
            var delegateType = typeof(Action<,>).MakeGenericType(typeof(long), sourceType!);
            var typedDelegate = Delegate.CreateDelegate(delegateType, method);
                
            var wrapperMethod = typeof(LiturgyNode)
                .GetMethod(nameof(BuildTabChangedLongWrapper_SignalFirst), BindingFlags.NonPublic | BindingFlags.Static)!
                .MakeGenericMethod(sourceType!);
            
            return ((Action<int, Control>?)wrapperMethod.Invoke(null, [typedDelegate]), sourceType);
        }
        else
        {
            var delegateType = typeof(Action<,>).MakeGenericType(sourceType!, typeof(long));
            var typedDelegate = Delegate.CreateDelegate(delegateType, method);
                
            var wrapperMethod = typeof(LiturgyNode)
                .GetMethod(nameof(BuildTabChangedLongWrapper_NodeFirst), BindingFlags.NonPublic | BindingFlags.Static)!
                .MakeGenericMethod(sourceType!);
            
            return ((Action<int, Control>?)wrapperMethod.Invoke(null, [typedDelegate]), sourceType);
        }

    }
    
    private static Action<int, Control> BuildTabChangedLongWrapper_SignalFirst<TSource>(Action<long, TSource> del)
        where TSource : Control
    {
        return (index, source) =>
        {
            if (source is TSource typed)
                del(index, typed);
        };
    }
    
    private static Action<int, Control> BuildTabChangedLongWrapper_NodeFirst<TSource>(Action<TSource, long> del)
        where TSource : Control
    {
        return (index, source) =>
        {
            if (source is TSource typed)
                del(typed, index);
        };
    }
    
    private void SubscribeOptionButton(OptionButton optionButton, string path)
    {
        if (!_subscribedOptionButtons.Add(optionButton)) return;
        var hasItemSelected = _itemSelectedGlobalCallbacks.Count > 0 || _itemSelectedTargetedCallbacks.ContainsKey(path);
        if (hasItemSelected) Listen(optionButton, OptionButton.SignalName.ItemSelected, Callable.From((long index) => OnOptionButtonItemSelected((int)index, optionButton, path)));
    }
    
    private void SubscribeItemList(ItemList itemList, string path)
    {
        if (!_subscribedItemLists.Add(itemList)) return;
        
        var hasItemSelected = _itemSelectedGlobalCallbacks.Count > 0 || _itemSelectedTargetedCallbacks.ContainsKey(path);
        var hasItemActivated = _itemActivatedGlobalCallbacks.Count > 0 || _itemActivatedTargetedCallbacks.ContainsKey(path);
        
        if (hasItemSelected) Listen(itemList, ItemList.SignalName.ItemSelected, Callable.From((long index) => OnItemListItemSelected((int)index, itemList, path)));
        if (hasItemActivated) Listen(itemList, ItemList.SignalName.ItemActivated, Callable.From((long index) => OnItemListItemActivated((int)index, itemList, path)));
    }
    
    private void SubscribeTabContainer(TabContainer tabContainer, string path)
    {
        if (!_subscribedTabContainers.Add(tabContainer)) return;
        var hasTabChanged = _tabChangedGlobalCallbacks.Count > 0 || _tabChangedTargetedCallbacks.ContainsKey(path);
        if (hasTabChanged) Listen(tabContainer, TabContainer.SignalName.TabChanged, Callable.From((long index) => OnTabContainerTabChanged((int)index, tabContainer, path)));
    }
    
    private void SubscribeTabBar(TabBar tabBar, string path)
    {
        if (!_subscribedTabBars.Add(tabBar)) return;
        var hasTabChanged = _tabChangedGlobalCallbacks.Count > 0 || _tabChangedTargetedCallbacks.ContainsKey(path);
        if (hasTabChanged) Listen(tabBar, TabBar.SignalName.TabChanged, Callable.From((long index) => OnTabBarTabChanged((int)index, tabBar, path)));
    }
    
    private void OnItemListItemSelected(int index, ItemList source, string path)
    {
        if (_itemSelectedTargetedCallbacks.TryGetValue(path, out var targeted))
        {
            foreach (var (callback, excludes) in targeted)
            {
                if (PathMatchesAnyExclude(path, excludes)) continue;
                callback(index, source);
            }
        }
        
        foreach (var (callback, excludes, sourceType) in _itemSelectedGlobalCallbacks)
        {
            if (PathMatchesAnyExclude(path, excludes)) continue;
            if (sourceType is not null && !sourceType.IsInstanceOfType(source)) continue;
            callback(index, source);
        }
    }
    
    private void OnOptionButtonItemSelected(int index, OptionButton source, string path)
    {
        if (_itemSelectedTargetedCallbacks.TryGetValue(path, out var targeted))
        {
            foreach (var (callback, excludes) in targeted)
            {
                if (PathMatchesAnyExclude(path, excludes)) continue;
                callback(index, source);
            }
        }
        
        foreach (var (callback, excludes, sourceType) in _itemSelectedGlobalCallbacks)
        {
            if (PathMatchesAnyExclude(path, excludes)) continue;
            if (sourceType is not null && !sourceType.IsInstanceOfType(source)) continue;
            callback(index, source);
        }
    }
    
    private void OnItemListItemActivated(int index, ItemList source, string path)
    {
        if (_itemActivatedTargetedCallbacks.TryGetValue(path, out var targeted))
        {
            foreach (var (callback, excludes) in targeted)
            {
                if (PathMatchesAnyExclude(path, excludes)) continue;
                callback(index, source);
            }
        }
        
        foreach (var (callback, excludes) in _itemActivatedGlobalCallbacks)
        {
            if (PathMatchesAnyExclude(path, excludes)) continue;
            callback(index, source);
        }
    }
    
    private void OnTabContainerTabChanged(int index, TabContainer source, string path)
    {
        if (_tabChangedTargetedCallbacks.TryGetValue(path, out var targeted))
        {
            foreach (var (callback, excludes) in targeted)
            {
                if (PathMatchesAnyExclude(path, excludes)) continue;
                callback(index, source);
            }
        }
        
        foreach (var (callback, excludes, sourceType) in _tabChangedGlobalCallbacks)
        {
            if (PathMatchesAnyExclude(path, excludes)) continue;
            if (sourceType is not null && !sourceType.IsInstanceOfType(source)) continue;
            callback(index, source);
        }
    }
    
    private void OnTabBarTabChanged(int index, TabBar source, string path)
    {
        if (_tabChangedTargetedCallbacks.TryGetValue(path, out var targeted))
        {
            foreach (var (callback, excludes) in targeted)
            {
                if (PathMatchesAnyExclude(path, excludes)) continue;
                callback(index, source);
            }
        }
        
        foreach (var (callback, excludes, sourceType) in _tabChangedGlobalCallbacks)
        {
            if (PathMatchesAnyExclude(path, excludes)) continue;
            if (sourceType is not null && !sourceType.IsInstanceOfType(source)) continue;
            callback(index, source);
        }
    }
}
