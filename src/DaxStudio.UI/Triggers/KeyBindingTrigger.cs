﻿﻿namespace DaxStudio.UI.Triggers
 {
     using System.Windows;
     using System.Windows.Input;
     using Microsoft.Xaml.Behaviors;

    public class KeyTrigger : TriggerBase<UIElement>
     {
         public static readonly DependencyProperty KeyProperty =
             DependencyProperty.Register("Key", typeof(Key), typeof(KeyTrigger), null);

         public static readonly DependencyProperty ModifiersProperty =
             DependencyProperty.Register("Modifiers", typeof(ModifierKeys), typeof(KeyTrigger), null);

         public Key Key
         {
             get { return (Key)GetValue(KeyProperty); }
             set { SetValue(KeyProperty, value); }
         }

         public ModifierKeys Modifiers
         {
             get { return (ModifierKeys)GetValue(ModifiersProperty); }
             set { SetValue(ModifiersProperty, value); }
         }

         protected override void OnAttached()
         {
             base.OnAttached();

             AssociatedObject.PreviewKeyDown += OnAssociatedObjectKeyDown;
         }

         protected override void OnDetaching()
         {
             base.OnDetaching();

             AssociatedObject.KeyDown -= OnAssociatedObjectKeyDown;
         }

         void OnAssociatedObjectKeyDown(object sender, KeyEventArgs e)
         {
             if ((e.Key == Key) && (Keyboard.Modifiers == GetActualModifiers(e.Key, Modifiers)))
             {
                 InvokeActions(e);
             }
         }

         static ModifierKeys GetActualModifiers(Key key, ModifierKeys modifiers)
         {
             if (key == Key.LeftCtrl || key == Key.RightCtrl)
             {
                 modifiers |= ModifierKeys.Control;
                 return modifiers;
             }

             if (key == Key.LeftAlt || key == Key.RightAlt)
             {
                 modifiers |= ModifierKeys.Alt;
                 return modifiers;
             }

             if (key == Key.LeftShift || key == Key.RightShift)
             {
                 modifiers |= ModifierKeys.Shift;
             }

             return modifiers;
         }
     }
 }