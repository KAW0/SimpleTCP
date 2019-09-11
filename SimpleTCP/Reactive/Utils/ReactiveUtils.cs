using System;
using System.Collections.Generic;
using System.Reactive.Disposables;
using System.Text;

namespace System.Reactive.Disposables
{
    public static class ReactiveUtils
    {
        public static void ComposeTo(this IDisposable disposable, CompositeDisposable composide)
        {
            composide.Add(disposable);
        }

    }
}
