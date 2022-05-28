using System;
using System.Linq.Expressions;
using System.Reflection;
using System.ComponentModel;

#nullable enable

namespace K4.Bind
{
	public abstract class Binding : IDisposable
	{
		public abstract void Dispose();

		public static Binding Create<T>(Expression<Func<T>> leftSide, Expression<Func<T>> rightSide, bool isTwoWay = true)
		{
			return new BindingImpl<T>(leftSide.Body, rightSide.Body, isTwoWay);
		}

		private class BindingImpl<T> : Binding
		{
			private readonly PropertyWrapper<T> leftWrapper;
			private readonly PropertyWrapper<T> rightWrapper;

			private readonly Action? leftChangedHandler;
			private readonly Action? rightChangedHandler;

			public BindingImpl(Expression left, Expression right, bool isTwoWay)
			{
				leftWrapper = PropertyWrapper<T>.Create(left);
				rightWrapper = PropertyWrapper<T>.Create(right);

				if(leftWrapper.CanGet && rightWrapper.CanSet)
				{
					leftChangedHandler = () => OnSideChanged(leftWrapper, rightWrapper);
					leftWrapper.OnChanged += leftChangedHandler;

					leftChangedHandler();
				}
				else
				{
					throw new ArgumentException("For both one-way and two-way binding, the left side should be readable and right side should be writeable. ");
				}

				if(isTwoWay)
				{
					if(rightWrapper.CanGet && leftWrapper.CanSet)
					{
						rightChangedHandler = () => OnSideChanged(rightWrapper, leftWrapper);
						rightWrapper.OnChanged += rightChangedHandler;
					}
					else
					{
						throw new ArgumentException("For two-way binding, the right side should be readable and left side should be writeable. ");
					}
				}

				static void OnSideChanged(PropertyWrapper<T> changingSide, PropertyWrapper<T> propagatingSide)
				{
					propagatingSide.Value = changingSide.Value;
				}
			}

			public override void Dispose()
			{
				if(leftChangedHandler != null)
				{
					leftWrapper.OnChanged -= leftChangedHandler;
				}
				leftWrapper.Dispose();

				if(rightChangedHandler != null)
				{
					rightWrapper.OnChanged -= rightChangedHandler;
				}
				rightWrapper.Dispose();
			}
		}

		private class PropertyWrapper<T> : IDisposable
		{
			public T Value
			{
				get
				{
					if(getter == null)
					{
						throw new InvalidOperationException("Property does not have a getter");
					}

					return getter();
				}
				set
				{
					setter?.Invoke(value);
				}
			}

			public bool CanSet => setter != null;
			public bool CanGet => getter != null;

			private delegate T Getter();
			private delegate void Setter(T value);

			public event Action? OnChanged;

			private readonly Setter? setter;
			private readonly Getter? getter;
			private readonly INotifyPropertyChanged? changeNotifier;
			private readonly string? name;

			public PropertyWrapper(object target, PropertyInfo property)
			{
				MethodInfo? getMethod = property.GetGetMethod();
				MethodInfo? setMethod = property.GetSetMethod();

				if(getMethod != null)
				{
					getter = (Getter)Delegate.CreateDelegate(typeof(Getter), target, getMethod, true);

					if(target is not INotifyPropertyChanged)
					{
						throw new ArgumentException($"Type \"{target.GetType().FullName}\" must implement {typeof(INotifyPropertyChanged).FullName} so that its property changes can be notified", nameof(target));
					}

					changeNotifier = (INotifyPropertyChanged)target;
					changeNotifier.PropertyChanged += OnPropertyChange;

					name = property.Name;
				}

				if(setMethod != null)
				{
					setter = (Setter)Delegate.CreateDelegate(typeof(Setter), target, setMethod, true);
				}
			}

			private void OnPropertyChange(object o, PropertyChangedEventArgs args)
			{
				if(args.PropertyName == name)
				{
					OnChanged?.Invoke();
				}
			}

			public void Dispose()
			{
				if(changeNotifier != null)
				{
					changeNotifier.PropertyChanged -= OnPropertyChange;
				}
			}

			public static PropertyWrapper<T> Create(Expression expr)
			{
				if(expr is not MemberExpression memberExpression)
				{
					throw new ArgumentException("Provided expression must be a member access expression", nameof(expr));
				}

				if(memberExpression.Member is not PropertyInfo property)
				{
					throw new ArgumentException($"The accessed member \"{memberExpression.Member.Name}\" must be property ", nameof(expr));
				}

				var lambda = Expression.Lambda(memberExpression.Expression, Array.Empty<ParameterExpression>());
				object target = lambda.Compile().DynamicInvoke();

				return new(target, property);
			}
		}
	}
}
