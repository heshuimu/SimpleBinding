# SimpleBinding
A very simple One-Way or Two-Way binding class for C#

# Acknowledgement
This is inspired by [praeclarum/Bind](https://github.com/praeclarum/Bind) to use the lambda expression tree to get runtime type information while keeping the API as expressive as possible. 

The difference between my implementation and the source of inspiration is that, in order to keep things simple, it does not have support for property path. Also, since Microsoft MVVM Toolkit (part of [.NET Community Toolkit](https://github.com/CommunityToolkit/dotnet)) recently added source generator support that can add `INotifyPropertyChanged` boilerplate code to any class, my implementation expects all target objects to implement the interface, thus omitting support for searching via Reflection for events supported by the original implementation. 

The API is also changed to utilize the compile-time type checking to ensure both sides of the binding are of the same type. 

# Usage

```cs
// Two-way:
Binding binding = Binding.Create(() => objA.Foo, () => objB.Bar);
// One-way:
Binding binding = Binding.Create(() => objA.Foo, () => objB.Bar, isTwoWay: false);

// Unbind:
binding.Dispose();
```

# MVVM in Unity

I made this implementation mainly for using in Unity. With Unity 2021.3.0f1, This works both in Editor and iOS Player build under the .NET Standard 2.1 profile.

The following snippet shows how to use the binding and MVVM Toolkit to bind a `ScriptableObject` with a text field.
```cs
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using Microsoft.Toolkit.Mvvm.ComponentModel;
using K4.Bind;

namespace A
{
	[ObservableObject]
	public partial class MyViewModel : ScriptableObject
	{
		[ObservableProperty, SerializeField]
		private string test = "Some Text";
	}

	[INotifyPropertyChanged]
	public partial class Behavior : MonoBehaviour
	{
		public string Text
		{
			get => text.text;
			set => SetProperty(text.text, value, text, (model, newValue) => text.text = newValue);
		}

		[SerializeField]
		private MyViewModel model;
		[SerializeField]
		private Text text;

		private Binding binding;

		IEnumerator Start()
		{
			model = ScriptableObject.CreateInstance<MyViewModel>();
			model.PropertyChanged += (o, c) => Debug.Log(c.PropertyName);

			binding = Binding.Create(() => model.Test, () => Text, isTwoWay: true);

			yield return new WaitForSeconds(3);

			model.Test = "AAA";
		}

		void OnDestroy()
		{
			binding.Dispose();
		}

		// Invoked by a button
		public void UpdateText()
		{
			Text = "BBBB";
			Debug.Log(model.Test);
		}
	}
}
```

Along with the obvious benefit that most of the boilerplate code is no longer needed to be hand-written, thus MVVM infastructure can be easily added to any classes inheriting `MonoBehaviour` or `ScriptableObject`, because MVVM Toolkit can generate code from a field, many, if not all, of the Unity features are retained. 

To set up MVVM Toolkit in Unity:
1. Download [the latest NuGet package](https://www.nuget.org/packages/Microsoft.Toolkit.Mvvm/) 
2. Download [the compatible Unsafe package (dependency)](https://www.nuget.org/packages/System.Runtime.CompilerServices.Unsafe/)
4. Unzip the packages
5. Move the .NET Standard DLLs into your Unity project, and...
6. Configure the source generator DLL as per [Unity's instruction](https://docs.unity3d.com/Manual/roslyn-analyzers.html).
