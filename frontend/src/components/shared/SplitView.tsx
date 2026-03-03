import { makeStyles, mergeClasses } from "@fluentui/react-components";
import type React from "react";

const useStyles = makeStyles({
  root: {
    height: "100%",
    display: "flex",
    flexDirection: "row",
  },
  first: {
    maxHeight: "100%",
    overflowY: "auto",
  },
  second: {
    flexGrow: 1,
    maxHeight: "100%",
    overflowY: "auto",
  },
});

interface SplitViewProps {
  firstPaneClassName?: string;
  first: React.ReactNode;
  secondPaneClassName?: string;
  second?: React.ReactNode;
  containerClassName?: string;
}

export function SplitView({
  firstPaneClassName,
  first,
  secondPaneClassName,
  second,
  containerClassName,
}: SplitViewProps) {
  const styles = useStyles();

  return (
    <div
      className={
        containerClassName
          ? mergeClasses(styles.root, containerClassName)
          : styles.root
      }
    >
      {first != null ? (
        <div className={mergeClasses(styles.first, firstPaneClassName)}>
          {first}
        </div>
      ) : null}
      {second != null ? (
        <div className={mergeClasses(styles.second, secondPaneClassName)}>
          {second}
        </div>
      ) : null}
    </div>
  );
}
